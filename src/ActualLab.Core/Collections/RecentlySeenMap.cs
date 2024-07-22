using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Collections;

public sealed class RecentlySeenMap<TKey, TValue>(
    int capacity,
    TimeSpan duration,
    MomentClock? clock = null)
    where TKey : notnull
{
    private readonly BinaryHeap<Moment, TKey> _heap = new(capacity + 1); // we may add one extra item, so "+ 1"
    private readonly Dictionary<TKey, TValue> _map = new(capacity + 1); // we may add one extra item, so "+ 1"

    public int Capacity { get; } = capacity;
    public TimeSpan Duration { get; } = duration;
    public MomentClock Clock { get; } = clock ?? MomentClockSet.Default.SystemClock;

    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue existingValue)
        => _map.TryGetValue(key, out existingValue);

    public bool TryAdd(TKey key, TValue value = default!)
        => TryAdd(key, Clock.Now, value);

    public bool TryAdd(TKey key, Moment timestamp, TValue value = default!)
    {
        if (!_map.TryAdd(key, value))
            return false;

        _heap.Add(timestamp, key);
        Prune();
        return true;
    }

    public bool TryRemove(TKey key)
    {
        if (!_map.Remove(key))
            return false;

        while (_heap.PeekMin().IsSome(out var value) && !_map.ContainsKey(value.Value))
            _heap.ExtractMin();
        return true;
    }

    public void Prune()
    {
        // Removing some items while there are too many
        while (_heap.Count > Capacity) {
            if (_heap.ExtractMin().IsSome(out var entry))
                _map.Remove(entry.Value);
            else
                break;
        }

        // Removing too old items
        var minTimestamp = Clock.Now - Duration;
        while (_heap.PeekMin().IsSome(out var entry) && entry.Priority < minTimestamp) {
            _heap.ExtractMin();
            _map.Remove(entry.Value);
        }
    }
}
