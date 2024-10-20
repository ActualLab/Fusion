using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Tests.Caching.Alternative;

#pragma warning disable RCS1059

public sealed class ArrayGrowOnlyCache<TKey, TValue> : GrowOnlyCache<TKey, TValue>
    where TKey : notnull
{
    private const int MaxCapacity = 50;
    private volatile Item[] _items;

    public ArrayGrowOnlyCache(IEqualityComparer<TKey> comparer) : base(comparer)
        => _items = [];

    public ArrayGrowOnlyCache(GrowOnlyCache<TKey, TValue> source) : base(source)
    {
        _items = source.Select(x => new Item(Comparer.GetHashCode(x.Key), x.Key, x.Value)).ToArray();
        _items.SortInPlace(x => x.HashCode);
    }

    public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        => BinarySearch(_items, key, out value) >= 0;

    public override TValue GetOrAdd(
        ref GrowOnlyCache<TKey, TValue> cache, TKey key, Func<TKey, TValue> valueFactory)
    {
        if (BinarySearch(_items, key,out var value) >= 0)
            return value;

        lock (Lock) {
            var index = BinarySearch(_items, key, out value);
            if (index >= 0)
                return value;

            if (_items.Length > MaxCapacity) {
                cache = new DictionaryGrowOnlyCache<TKey, TValue>(this);
                return cache.GetOrAdd(ref cache, key, valueFactory);
            }

            var newItems = new Item[_items.Length + 1];
            value = valueFactory.Invoke(key);
            index = ~index;
            newItems[index] = new(Comparer.GetHashCode(key), key, value);
            _items.AsSpan(0, index).CopyTo(newItems.AsSpan());
            _items.AsSpan(index).CopyTo(newItems.AsSpan(index + 1));
            _items = newItems;
            return value;
        }
    }

    public override TValue GetOrAdd<TState>(
        ref GrowOnlyCache<TKey, TValue> cache, TKey key, Func<TKey, TState, TValue> valueFactory, TState state)
    {
        if (BinarySearch(_items, key, out var value) >= 0)
            return value;

        lock (Lock) {
            var index = BinarySearch(_items, key, out value);
            if (index >= 0)
                return value;

            if (_items.Length > MaxCapacity) {
                cache = new DictionaryGrowOnlyCache<TKey, TValue>(this);
                return cache.GetOrAdd(ref cache, key, valueFactory, state);
            }

            var newItems = new Item[_items.Length + 1];
            value = valueFactory.Invoke(key, state);
            index = ~index;
            newItems[index] = new(Comparer.GetHashCode(key), key, value);
            _items.AsSpan(0, index).CopyTo(newItems.AsSpan());
            _items.AsSpan(index).CopyTo(newItems.AsSpan(index + 1));
            _items = newItems;
            return value;
        }
    }

    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        => _items.Select(i => new KeyValuePair<TKey,TValue>(i.Key, i.Value)).GetEnumerator();

    // Private methods

    private int BinarySearch(Item[] items, TKey key, out TValue value)
    {
        var hashCode = Comparer.GetHashCode(key);
        var index = BinarySearch(items, hashCode);
        if (index < 0) {
            value = default!;
            return index;
        }

        for (var i = index; i < items.Length; i++) {
            ref var item = ref items[i];
            if (item.HashCode != hashCode)
                break;

            if (Comparer.Equals(key, item.Key)) {
                value = items[i].Value;
                return i;
            }
        }

        value = default!;
        return ~index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BinarySearch(Item[] items, int hashCode)
    {
        var result = -1;
        var left = 0;
        var right = items.Length - 1;
        while (left <= right) {
            var mid = left + ((right - left) >> 1);
            var midHashCode = items[mid].HashCode;
            if (midHashCode == hashCode) {
                result = mid;
                right = mid - 1;
            }
            else if (midHashCode < hashCode)
                left = mid + 1; // Search in the right half
            else
                right = mid - 1; // Search in the left half
        }
        if (result < 0)
            result = ~left;
        return result;
    }

    // Nested types

    [StructLayout(LayoutKind.Auto)]
    private sealed record Item(int HashCode, TKey Key, TValue Value);
}
