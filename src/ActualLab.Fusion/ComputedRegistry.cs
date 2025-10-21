using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using ActualLab.Concurrency;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Locking;
using ActualLab.OS;
using Errors = ActualLab.Fusion.Internal.Errors;

namespace ActualLab.Fusion;

public sealed class ComputedRegistry
{
    internal static readonly MeterSet Metrics;
    public static ComputedRegistry Instance { get; set; }

    public sealed record Options
    {
        public int InitialCapacity { get; init; } = FusionDefaults.ComputedRegistryInitialCapacity;
        public int ConcurrencyLevel { get; init; } = FusionDefaults.ComputedRegistryConcurrencyLevel;
        public Func<AsyncLockSet<ComputedInput>>? LocksFactory { get; init; } = null;
    }

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private StochasticCounter _opCounter;
    private readonly ConcurrentDictionary<ComputedInput, GCHandle> _storage;
    private ComputedGraphPruner? _graphPruner;
    private int _pruneOpCounterThreshold;
    private Task? _pruneTask;

    [field: AllowNull, MaybeNull]
    private ILogger Log => field ??= StaticLog.For<ComputedRegistry>();

    public IEnumerable<ComputedInput> Keys => _storage.Select(p => p.Key);
    public AsyncLockSet<ComputedInput> InputLocks { get; }
    public ComputedGraphPruner? GraphPruner => _graphPruner;

    public event Action<Computed>? OnRegister;
    public event Action<Computed>? OnUnregister;
    public event Action<Computed, bool>? OnAccess;

    static ComputedRegistry()
    {
        Metrics = new();
        Instance = new ComputedRegistry();
        var computedGraphPruner = new ComputedGraphPruner(ComputedGraphPruner.Options.Default);
        if (!computedGraphPruner.Settings.AutoActivate)
            computedGraphPruner.Start();
    }

    public ComputedRegistry() : this(new()) { }
    public ComputedRegistry(Options settings)
    {
        _opCounter = new StochasticCounter(HardwareInfo.GetProcessorCountPo2Factor(4));
        _storage = new ConcurrentDictionary<ComputedInput, GCHandle>(
            settings.ConcurrencyLevel,
            settings.InitialCapacity,
            ComputedInput.EqualityComparer);
        InputLocks = settings.LocksFactory?.Invoke() ?? new AsyncLockSet<ComputedInput>(
            LockReentryMode.CheckedFail,
            settings.ConcurrencyLevel,
            Math.Max(1, settings.InitialCapacity / 4),
            ComputedInput.EqualityComparer);
        UpdatePruneCounterThreshold(out _);
    }

    public Computed? Get(ComputedInput key)
    {
        var random = key.HashCode + Environment.CurrentManagedThreadId;
        OnOperation(random);
        if (!_storage.TryGetValue(key, out var handle))
            return null;

        var target = handle.Target;
        switch (target) {
        case null:
            if (_storage.TryRemove(key, handle))
                handle.Free();
            return null;
        case Computed computed:
            return computed;
        default:
            // Invalid GCHandle.Target, the best we can do is to remove it
            _storage.TryRemove(key, handle);
            LogInvalidGCHandleTargetSafely(target);
            return null;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Register(Computed computed)
    {
        // Debug.WriteLine($"{nameof(Register)}: {computed}");

        var key = computed.Input;
        var random = key.HashCode + Environment.CurrentManagedThreadId;
        OnRegister?.Invoke(computed);
        OnOperation(random);

        var spinWait = new SpinWait();
        GCHandle? newHandle = null;
        while (computed.ConsistencyState != ConsistencyState.Invalidated) {
            if (_storage.TryGetValue(key, out var handle)) {
                var target = handle.Target;
                if (ReferenceEquals(target, computed)) {
                    newHandle?.Free();
                    return;
                }

                switch (target) {
                case null:
                    goto tryRemove;
                case Computed cTarget:
                    if (cTarget is { ConsistencyState: not ConsistencyState.Invalidated }) {
                        try {
                            // This typically triggers Unregister - except for RemoteComputed
                            cTarget.Invalidate();
                        }
                        catch {
                            newHandle?.Free();
                            throw;
                        }
                    }
                    goto tryRemove;
                default:
                    // Invalid GCHandle.Target, the best we can do is to remove it
                    _storage.TryRemove(key, handle);
                    LogInvalidGCHandleTargetSafely(target);
                    continue;
                }

                tryRemove:
                if (_storage.TryRemove(key, handle))
                    handle.Free();
            }
            else {
                newHandle ??= GCHandle.Alloc(computed, GCHandleType.Weak);
                if (_storage.TryAdd(key, newHandle.GetValueOrDefault()))
                    return;
            }
            spinWait.SpinOnce(); // Safe for WASM
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Unregister(Computed computed)
    {
        // We can't remove what still could be invalidated,
        // since "usedBy" links are resolved via this registry
        if (computed.ConsistencyState != ConsistencyState.Invalidated)
            throw Errors.WrongComputedState(computed.ConsistencyState);

        var key = computed.Input;
        var random = key.HashCode + Environment.CurrentManagedThreadId;
        OnUnregister?.Invoke(computed);
        OnOperation(random);

        if (!_storage.TryGetValue(key, out var handle))
            return;

        var target = handle.Target;
        if (!(ReferenceEquals(target, computed) || ReferenceEquals(target, null)))
            return;

        // handle.Target is null (is gone, i.e. to be pruned)
        // or pointing to the right computation object
        if (_storage.TryRemove(key, handle))
            handle.Free();

        // If another thread removed the entry, it also released the handle
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PseudoRegister(Computed computed)
        => OnRegister?.Invoke(computed);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PseudoUnregister(Computed computed)
        => OnUnregister?.Invoke(computed);

    public void InvalidateEverything()
    {
        var keys = _storage.Keys.ToList();
        foreach (var key in keys)
            Get(key)?.Invalidate();
    }

    public Task Prune()
    {
        lock (_lock) {
            if (_pruneTask is null || _pruneTask.IsCompleted) {
                using var _ = ExecutionContextExt.TrySuppressFlow();
                _pruneTask = Task.Run(PruneUnsafe);
            }
            return _pruneTask;
        }
    }

    public ComputedGraphPruner? ChangeGraphPruner(
        ComputedGraphPruner? graphPruner,
        ComputedGraphPruner? expectedGraphPruner)
    {
        var oldGraphPruner = Interlocked.CompareExchange(ref _graphPruner, graphPruner, expectedGraphPruner);
        if (oldGraphPruner != expectedGraphPruner)
            return oldGraphPruner;

        graphPruner?.Start();
        return graphPruner;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReportAccess(Computed computed, bool isNew)
    {
        if (OnAccess is not null && computed.Input.Function is ComputeMethodFunction)
            OnAccess.Invoke(computed, isNew);
    }

    // Private methods

    private void OnOperation(int random)
    {
        if (_opCounter.Increment(random) is not { } c || c <= _pruneOpCounterThreshold)
            return;
        if (_opCounter.Reset() <= _pruneOpCounterThreshold)
            return;

        _ = Prune();
    }

    private void PruneUnsafe()
    {
        var startedAt = CpuTimestamp.Now;
        Metrics.KeyPruneCount.Add(1);

        // Debug.WriteLine(nameof(PruneInternal));
        var prunedKeyCount = 0L;

        foreach (var (key, handle) in _storage) {
            var target = handle.Target;
            switch (target) {
            case null:
                if (_storage.TryRemove(key, handle)) {
                    handle.Free();
                    prunedKeyCount++;
                }
                break;
            case Computed:
                break;
            default:
                // Invalid GCHandle.Target, the best we can do is to remove it
                if (_storage.TryRemove(key, handle))
                    prunedKeyCount++;
                LogInvalidGCHandleTargetSafely(target);
                break;
            }
        }

        int keyCount;
        lock (_lock) {
            UpdatePruneCounterThreshold(out keyCount);
            _opCounter.Reset();
        }
        Interlocked.Exchange(ref Metrics.KeyCount, keyCount);
        Metrics.PrunedKeyCount.Add(prunedKeyCount);
        Metrics.KeyPruneDuration.Record(startedAt.Elapsed.TotalMilliseconds);
    }

    private void UpdatePruneCounterThreshold(out int keyCount)
    {
        lock (_lock) {
            // Should be called inside Lock
            keyCount = _storage.Count;
            var nextThreshold = keyCount << 1; // x2
            if (nextThreshold < keyCount) // Overflow
                nextThreshold = int.MaxValue;
            nextThreshold = nextThreshold.Clamp(1024, int.MaxValue >> 1);
            _pruneOpCounterThreshold = nextThreshold;
        }
    }

    // ReSharper disable once InconsistentNaming
    private void LogInvalidGCHandleTargetSafely(object target)
    {
        // When this method is called, the handle points to a type other than Computed,
        // which means there is an issue with GCHandle usage in the app.
        // E.g., some other code uses Fusion's GCHandles.
        try {
            Log.LogWarning("GCHandle.Target is of {Type} instead of Computed", target.GetType().FullName);
        }
        catch {
            // Intended
        }
    }

    // Nested types

    public class MeterSet
    {
        public readonly ObservableCounter<long> CapacityCounter;
        public readonly ObservableCounter<long> NodeCounter;
        public readonly ObservableCounter<long> EdgeCounter;
        public readonly Counter<long> PrunedKeyCount;
        public readonly Counter<long> PrunedDisposedCount;
        public readonly Counter<long> PrunedEdgeCount;
        public readonly Counter<long> KeyPruneCount;
        public readonly Counter<long> NodeEdgePruneCount;
        public readonly Histogram<double> KeyPruneDuration;
        public readonly Histogram<double> NodePruneDuration;
        public readonly Histogram<double> EdgePruneDuration;
        public long KeyCount;
        public long NodeCount;
        public long EdgeCount;

        public MeterSet()
        {
            var m = FusionInstruments.Meter;
            var ms = "computed.registry";
            CapacityCounter = m.CreateObservableCounter($"{ms}.key.count",
                () => Interlocked.Read(ref KeyCount),
                null, "ComputedRegistry key count.");
            NodeCounter = m.CreateObservableCounter($"{ms}.node.count",
                () => Interlocked.Read(ref NodeCount),
                null, "Count of nodes in Computed<T> dependency graph.");
            EdgeCounter = m.CreateObservableCounter($"{ms}.edge.count",
                () => Interlocked.Read(ref EdgeCount),
                null, "Count of edges in Computed<T> dependency graph.");

            PrunedKeyCount = m.CreateCounter<long>($"{ms}.pruned.key.count",
                null, "Count of pruned Computed<T> instances.");
            PrunedDisposedCount = m.CreateCounter<long>($"{ms}.pruned.disposed.count",
                null, "Count of pruned disposable Computed<T> instances.");
            PrunedEdgeCount = m.CreateCounter<long>($"{ms}.pruned.edge.count",
                null, "Count of pruned edges in Computed<T> dependency graph.");

            KeyPruneCount = m.CreateCounter<long>($"{ms}.prunes.key-cycle.count",
                null, "Count of computed registry key prune cycles.");
            NodeEdgePruneCount = m.CreateCounter<long>($"{ms}.prunes.node-edge-cycle.count",
                null, "Count of computed registry node & edge prune cycles.");
            KeyPruneDuration = m.CreateHistogram<double>($"{ms}.prunes.key-cycle.duration",
                "ms", "Duration of computed registry key prune cycle.");
            NodePruneDuration = m.CreateHistogram<double>($"{ms}.prunes.node-cycle.duration",
                "ms", "Duration of computed registry graph node prune cycle.");
            EdgePruneDuration = m.CreateHistogram<double>($"{ms}.prunes.edge-cycle.duration",
                "ms", "Duration of computed registry graph edge prune cycle.");
        }
    }
}
