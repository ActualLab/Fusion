using ActualLab.Concurrency;
using ActualLab.OS;

namespace ActualLab.Collections;

/// <summary>
/// A capacity-bounded cache with lock-free reads. Once it grows past <see cref="Capacity"/>,
/// a background prune evicts arbitrary entries until half of that capacity is left.
/// </summary>
public sealed class PruningCache<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _storage;
    private readonly int _lowWaterMark;
#if NET9_0_OR_GREATER
    private readonly Lock _pruneLock = new();
#else
    private readonly object _pruneLock = new();
#endif
    private StochasticCounter _opCounter;
    private Task? _pruneTask;
    private int _pruneIndex;

    public int Capacity { get; }
    public int Count => _storage.Count;

    public PruningCache(int capacity, IEqualityComparer<TKey>? keyComparer = null, int concurrencyLevel = 0)
    {
        if (capacity < 2)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be 2 or greater.");

        Capacity = capacity;
        _lowWaterMark = capacity >> 1;
        // The counter carries no count of its own - it just gates how often the accurate
        // (and thus all-locks-acquiring) Count check runs. Sampling one add in ~1/16 of the
        // capacity keeps that check rare while capping the overshoot; small capacities, which
        // can't afford any overshoot, end up checking on every add.
        var precisionHint = (capacity >> 4).Clamp(1, HardwareInfo.GetProcessorCountPo2Factor(4));
        _opCounter = new StochasticCounter(precisionHint);
        _storage = new ConcurrentDictionary<TKey, TValue>(
            concurrencyLevel > 0 ? concurrencyLevel : HardwareInfo.ProcessorCountPo2,
            Math.Min(capacity, 131),
            keyComparer);
    }

    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value)
        => _storage.TryGetValue(key, out value);

    public bool TryAdd(TKey key, TValue value)
    {
        if (!_storage.TryAdd(key, value))
            return false;

        var random = key.GetHashCode() + Environment.CurrentManagedThreadId;
        if (_opCounter.Increment(random) is null)
            return true;

        _opCounter.Reset();
        if (_storage.Count > Capacity)
            _ = Prune();

        return true;
    }

    public Task Prune()
    {
        lock (_pruneLock) {
            if (_pruneTask is null || _pruneTask.IsCompleted) {
                using var _ = ExecutionContextExt.TrySuppressFlow();
                _pruneTask = Task.Run(PruneUnsafe);
            }
            return _pruneTask;
        }
    }

    // Private methods

    private void PruneUnsafe()
    {
        // The outer loop matters: entries added while this prune runs find it in flight and skip
        // triggering another one, so it has to re-check the count before it gives up.
        while (true) {
            var count = _storage.Count;
            if (count <= Capacity)
                return;

            // Which entries go is arbitrary by design: tracking their use would require a write on
            // every read, which is exactly what this cache avoids. The victim bit rotates from prune
            // to prune, so no key is a permanent victim; the second pass ignores it and evicts
            // whatever it enumerates, in case the first one didn't free enough.
            var victimBit = 1 << (_pruneIndex++ & 15);
            for (var pass = 0; pass < 2 && count > _lowWaterMark; pass++) {
                foreach (var entry in _storage) {
                    if (count <= _lowWaterMark)
                        break;

                    if (pass == 0 && (entry.Key.GetHashCode() & victimBit) == 0)
                        continue;

                    if (_storage.TryRemove(entry.Key, out _))
                        count--;
                }
            }
        }
    }
}
