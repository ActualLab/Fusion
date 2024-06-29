using ActualLab.Concurrency;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Locking;
using ActualLab.OS;
using ActualLab.Time.Internal;
using Errors = ActualLab.Fusion.Internal.Errors;

namespace ActualLab.Fusion;

public sealed class ComputedRegistry : IDisposable
{
    public static ComputedRegistry Instance { get; set; } = new();

    public sealed record Options
    {
        public int InitialCapacity { get; init; } = FusionDefaults.ComputedRegistryCapacity;
        public int ConcurrencyLevel { get; init; } = FusionDefaults.ComputedRegistryConcurrencyLevel;
        public Func<AsyncLockSet<ComputedInput>>? LocksFactory { get; init; } = null;
        // ReSharper disable once InconsistentNaming
        public GCHandlePool? GCHandlePool { get; init; } = null;
    }

    private readonly ConcurrentDictionary<ComputedInput, GCHandle> _storage;
    private readonly GCHandlePool _gcHandlePool;
    private StochasticCounter _opCounter;
    private volatile ComputedGraphPruner _graphPruner = null!;
    private volatile int _pruneOpCounterThreshold;
    private Task? _pruneTask;
    private object Lock => _storage;

    public IEnumerable<ComputedInput> Keys => _storage.Select(p => p.Key);
    public AsyncLockSet<ComputedInput> InputLocks { get; }
    public ComputedGraphPruner GraphPruner => _graphPruner;

    public event Action<Computed>? OnRegister;
    public event Action<Computed>? OnUnregister;
    public event Action<Computed, bool>? OnAccess;

    public ComputedRegistry() : this(new()) { }
    public ComputedRegistry(Options settings)
    {
        _storage = new ConcurrentDictionary<ComputedInput, GCHandle>(
            settings.ConcurrencyLevel,
            settings.InitialCapacity,
            ComputedInput.EqualityComparer);
        _gcHandlePool = settings.GCHandlePool ?? new GCHandlePool(GCHandleType.Weak);
        if (_gcHandlePool.HandleType != GCHandleType.Weak)
            throw new ArgumentOutOfRangeException(
                $"{nameof(settings)}.{nameof(settings.GCHandlePool)}.{nameof(_gcHandlePool.HandleType)}");

        _opCounter = new StochasticCounter(HardwareInfo.GetProcessorCountPo2Factor(4));
        InputLocks = settings.LocksFactory?.Invoke() ?? new AsyncLockSet<ComputedInput>(
            LockReentryMode.CheckedFail,
            settings.ConcurrencyLevel,
            settings.InitialCapacity,
            ComputedInput.EqualityComparer);
        ChangeGraphPruner(new ComputedGraphPruner(new()), null!);
        UpdatePruneCounterThreshold();
    }

    public void Dispose()
        => _gcHandlePool.Dispose();

    public Computed? Get(ComputedInput key)
    {
        var random = key.HashCode + Environment.CurrentManagedThreadId;
        OnOperation(random);
        if (_storage.TryGetValue(key, out var handle)) {
            var value = (Computed?)handle.Target;
            if (value != null)
                return value;

            if (_storage.TryRemove(key, handle))
                _gcHandlePool.Release(handle, random);
        }
        return null;
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
                var target = (Computed?) handle.Target;
                if (target == computed) {
                    if (newHandle.HasValue)
                        _gcHandlePool.Release(newHandle.Value, random);
                    return;
                }
                if (target is { ConsistencyState: not ConsistencyState.Invalidated }) {
                    // This typically triggers Unregister - except for RemoteComputed
                    target.Invalidate();
                }
                if (_storage.TryRemove(key, handle))
                    _gcHandlePool.Release(handle, random);
            }
            else {
                newHandle ??= _gcHandlePool.Acquire(computed, random);
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
        if (target != null && !ReferenceEquals(target, computed))
            return;

        // gcHandle.Target == null (is gone, i.e. to be pruned)
        // or pointing to the right computation object
        if (!_storage.TryRemove(key, handle))
            // If another thread removed the entry, it also released the handle
            return;

        _gcHandlePool.Release(handle, random);
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
        lock (Lock) {
            if (_pruneTask == null || _pruneTask.IsCompleted) {
                using var _ = ExecutionContextExt.TrySuppressFlow();
                _pruneTask = Task.Run(PruneUnsafe);
            }
            return _pruneTask;
        }
    }

    public ComputedGraphPruner ChangeGraphPruner(
        ComputedGraphPruner graphPruner,
        ComputedGraphPruner expectedGraphPruner)
    {
        var oldGraphPruner = Interlocked.CompareExchange(ref _graphPruner, graphPruner, expectedGraphPruner);
        if (oldGraphPruner != expectedGraphPruner)
            return oldGraphPruner;

        graphPruner.Start();
        return graphPruner;
    }

    public void ReportAccess(Computed computed, bool isNew)
    {
        if (OnAccess != null && computed.Input.Function is IComputeMethodFunction)
            OnAccess.Invoke(computed, isNew);
    }

    // Private methods

    private void OnOperation(int random)
    {
        if (_opCounter.Increment(random) is { } c && c > _pruneOpCounterThreshold)
            TryPrune();
    }

    private void TryPrune()
    {
        lock (Lock) {
            if (_opCounter.Value <= _pruneOpCounterThreshold) return;

            _opCounter.Value = 0;
            _ = Prune();
        }
    }

    private void PruneUnsafe()
    {
        var type = GetType();
        using var activity = type.GetActivitySource().StartActivity(type, nameof(Prune));

        // Debug.WriteLine(nameof(PruneInternal));
        var randomOffset = Environment.CurrentManagedThreadId + CoarseClockHelper.RandomInt32;
        foreach (var (key, handle) in _storage) {
            if (handle.Target == null && _storage.TryRemove(key, handle))
                _gcHandlePool.Release(handle, key.HashCode + randomOffset);
        }
        lock (Lock) {
            UpdatePruneCounterThreshold();
            _opCounter.Value = 0;
        }
    }

    private void UpdatePruneCounterThreshold()
    {
        lock (Lock) {
            // Should be called inside Lock
            var capacity = _storage.GetCapacity();
            var doubleCapacity = Math.Max(capacity, capacity << 1);
            var nextThreshold = doubleCapacity.Clamp(1024, int.MaxValue >> 1);
            _pruneOpCounterThreshold = nextThreshold;
        }
    }
}
