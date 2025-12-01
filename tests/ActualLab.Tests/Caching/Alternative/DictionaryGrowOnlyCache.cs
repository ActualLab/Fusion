namespace ActualLab.Tests.Caching.Alternative;

#pragma warning disable RCS1059, MA0064

public sealed class DictionaryGrowOnlyCache<TKey, TValue> : GrowOnlyCache<TKey, TValue>
    where TKey : notnull
{
    private volatile Dictionary<TKey, TValue> _items;

    public DictionaryGrowOnlyCache(IEqualityComparer<TKey> comparer) : base(comparer)
        => _items = new Dictionary<TKey, TValue>(comparer);

    public DictionaryGrowOnlyCache(GrowOnlyCache<TKey, TValue> source) : base(source)
    {
        _items = new Dictionary<TKey, TValue>(source.Comparer);
        foreach (var (key, value) in source)
            _items.Add(key, value);
    }

    public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        => _items.TryGetValue(key, out value);

    public override TValue GetOrAdd(
        ref GrowOnlyCache<TKey, TValue> cache, TKey key, Func<TKey, TValue> valueFactory)
    {
        if (_items.TryGetValue(key, out var value))
            return value;

        lock (Lock) {
            if (_items.TryGetValue(key, out value))
                return value;

            value = valueFactory.Invoke(key);
            var newItems = new Dictionary<TKey, TValue>(_items, Comparer) {
                [key] = value,
            };
            _items = newItems;
            return value;
        }
    }

    public override TValue GetOrAdd<TState>(
        ref GrowOnlyCache<TKey, TValue> cache, TKey key, Func<TKey, TState, TValue> valueFactory, TState state)
    {
        if (_items.TryGetValue(key, out var value))
            return value;

        lock (Lock) {
            if (_items.TryGetValue(key, out value))
                return value;

            value = valueFactory.Invoke(key, state);
            var newItems = new Dictionary<TKey, TValue>(_items, Comparer) {
                [key] = value,
            };
            _items = newItems;
            return value;
        }
    }

    public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        => _items.GetEnumerator();
}
