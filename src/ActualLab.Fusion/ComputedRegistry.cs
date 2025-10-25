using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using ActualLab.Concurrency;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Internal;
using ActualLab.Locking;
using ActualLab.OS;
using Errors = ActualLab.Fusion.Internal.Errors;

namespace ActualLab.Fusion;

public sealed class ComputedRegistry
{
    public static class Settings
    {
        public static int InitialCapacity { get; set; }
        public static int ConcurrencyLevel { get; set; }
        public static Func<AsyncLockSet<ComputedInput>>? LocksFactory { get; set; } = null;
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public static ILogger? Log { get; set; }

        static Settings()
        {
            var isServer = RuntimeInfo.IsServer;
            var cpuCountPo2 = HardwareInfo.ProcessorCountPo2;
            ConcurrencyLevel = (cpuCountPo2 * (isServer ? 8 : 1)).Clamp(1, 8192);
            var computedRegistryCapacityBase = (ConcurrencyLevel * 32).Clamp(256, 8192);
            InitialCapacity = PrimeSieve.GetPrecomputedPrime(computedRegistryCapacityBase);
        }
    }

    internal static readonly MeterSet Metrics;

#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static StochasticCounter _opCounter;
    private static readonly ConcurrentDictionary<ComputedInput, WeakReference<Computed>> _storage;
    private static ComputedGraphPruner? _graphPruner;
    private static int _pruneOpCounterThreshold;
    private static Task? _pruneTask;

    [field: AllowNull, MaybeNull]
    private static ILogger Log => field ??= Settings.Log ?? StaticLog.For<ComputedRegistry>();

    public static IEnumerable<ComputedInput> Keys => _storage.Select(p => p.Key);
    public static AsyncLockSet<ComputedInput> InputLocks { get; }
    public static ComputedGraphPruner? GraphPruner => _graphPruner;

    public static event Action<Computed>? OnRegister;
    public static event Action<Computed>? OnUnregister;
    public static event Action<Computed, bool>? OnAccess;

    static ComputedRegistry()
    {
        Metrics = new();
        _opCounter = new StochasticCounter(HardwareInfo.GetProcessorCountPo2Factor(4));
        _storage = new ConcurrentDictionary<ComputedInput, WeakReference<Computed>>(
            Settings.ConcurrencyLevel,
            Settings.InitialCapacity,
            ComputedInput.EqualityComparer);
        InputLocks = Settings.LocksFactory?.Invoke() ?? new AsyncLockSet<ComputedInput>(
            LockReentryMode.CheckedFail,
            Settings.ConcurrencyLevel,
            Math.Max(1, Settings.InitialCapacity / 4),
            ComputedInput.EqualityComparer);
        UpdatePruneCounterThreshold(out _);
        _graphPruner = new ComputedGraphPruner();
    }

    public static Computed? Get(ComputedInput key)
    {
        var random = key.HashCode + Environment.CurrentManagedThreadId;
        OnOperation(random);
        if (!_storage.TryGetValue(key, out var weakRef))
            return null;

        if (weakRef.TryGetTarget(out var target))
            return target;

        _storage.TryRemove(key, weakRef);
        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Register(Computed computed)
    {
        // Debug.WriteLine($"{nameof(Register)}: {computed}");

        var key = computed.Input;
        var random = key.HashCode + Environment.CurrentManagedThreadId;

        try {
            OnRegister?.Invoke(computed);
        }
        catch (Exception e) {
            LogOnXxxErrorSafely(nameof(OnRegister), e);
        }
        OnOperation(random);

        var spinWait = new SpinWait();
        WeakReference<Computed>? newWeakRef = null;
        while (computed.ConsistencyState != ConsistencyState.Invalidated) {
            if (_storage.TryGetValue(key, out var weakRef)) {
                weakRef.TryGetTarget(out var target);
                if (ReferenceEquals(target, computed))
                    return; // Already registered

                if (target is { ConsistencyState: not ConsistencyState.Invalidated })
                    target.Invalidate(); // This typically triggers Unregister - except for RemoteComputed

                _storage.TryRemove(key, weakRef);
            }
            else {
                newWeakRef ??= new WeakReference<Computed>(computed);
                if (_storage.TryAdd(key, newWeakRef))
                    return;
            }

            spinWait.SpinOnce(); // Safe for WASM
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Unregister(Computed computed)
    {
        // We can't remove what still could be invalidated,
        // since "usedBy" links are resolved via this registry
        if (computed.ConsistencyState != ConsistencyState.Invalidated)
            throw Errors.WrongComputedState(computed.ConsistencyState);

        var key = computed.Input;
        var random = key.HashCode + Environment.CurrentManagedThreadId;
        try {
            OnUnregister?.Invoke(computed);
        }
        catch (Exception e) {
            LogOnXxxErrorSafely(nameof(OnUnregister), e);
        }
        OnOperation(random);

        if (!_storage.TryGetValue(key, out var weakRef))
            return;

        weakRef.TryGetTarget(out var target);
        if (!(ReferenceEquals(target, computed) || ReferenceEquals(target, null)))
            return; // Points to some other computed

        // weakRef.Target is null (is gone, i.e. to be pruned)
        // or pointing to the right computed
        _storage.TryRemove(key, weakRef);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PseudoRegister(Computed computed)
        => OnRegister?.Invoke(computed);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PseudoUnregister(Computed computed)
        => OnUnregister?.Invoke(computed);

    public static void InvalidateEverything()
    {
        var keys = _storage.Keys.ToList();
        foreach (var key in keys)
            Get(key)?.Invalidate();
    }

    public static Task Prune()
    {
        lock (StaticLock) {
            if (_pruneTask is null || _pruneTask.IsCompleted) {
                using var _ = ExecutionContextExt.TrySuppressFlow();
                _pruneTask = Task.Run(PruneUnsafe);
            }
            return _pruneTask;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReportAccess(Computed computed, bool isNew)
    {
        if (OnAccess is not null && computed.Input.Function is ComputeMethodFunction)
            OnAccess.Invoke(computed, isNew);
    }

    // Internal methods

    internal static bool ChangeGraphPruner(ComputedGraphPruner? graphPruner, out ComputedGraphPruner? prevGraphPruner)
    {
        lock (StaticLock) {
            prevGraphPruner = _graphPruner;
            if (prevGraphPruner == _graphPruner)
                return false;

            _graphPruner = graphPruner;
            return true;
        }
    }

    // Private methods

    private static void OnOperation(int random)
    {
        if (_opCounter.Increment(random) is not { } c || c <= _pruneOpCounterThreshold)
            return;
        if (_opCounter.Reset() <= _pruneOpCounterThreshold)
            return;

        _ = Prune();
    }

    private static void PruneUnsafe()
    {
        var startedAt = CpuTimestamp.Now;
        Metrics.KeyPruneCount.Add(1);

        // Debug.WriteLine(nameof(PruneInternal));
        var prunedKeyCount = 0L;

        foreach (var (key, weakRef) in _storage) {
            if (weakRef.TryGetTarget(out _))
                continue; // Still alive

            if (_storage.TryRemove(key, weakRef))
                prunedKeyCount++;
        }

        int keyCount;
        lock (StaticLock) {
            UpdatePruneCounterThreshold(out keyCount);
            _opCounter.Reset();
        }
        Interlocked.Exchange(ref Metrics.KeyCount, keyCount);
        Metrics.PrunedKeyCount.Add(prunedKeyCount);
        Metrics.KeyPruneDuration.Record(startedAt.Elapsed.TotalMilliseconds);
    }

    private static void UpdatePruneCounterThreshold(out int keyCount)
    {
        lock (StaticLock) {
            // Should be called inside Lock
            keyCount = _storage.Count;
            var nextThreshold = keyCount << 1; // x2
            if (nextThreshold < keyCount) // Overflow
                nextThreshold = int.MaxValue;
            nextThreshold = nextThreshold.Clamp(1024, int.MaxValue >> 1);
            _pruneOpCounterThreshold = nextThreshold;
        }
    }

    private static void LogOnXxxErrorSafely(string eventName, Exception e)
    {
        try {
            Log.LogError(e, "{EventName} event handler threw an exception", eventName);
        }
        catch {
            // Intended: logging must not throw
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
