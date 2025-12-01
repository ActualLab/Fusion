namespace ActualLab.Tests.Caching.Alternative;

public abstract class GrowOnlyCache<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    where TKey : notnull
{
    protected readonly object Lock;

    public IEqualityComparer<TKey> Comparer { get; }

    public static GrowOnlyCache<TKey, TValue> New(IEqualityComparer<TKey>? comparer = null)
        => new ArrayGrowOnlyCache<TKey, TValue>(comparer ?? EqualityComparer<TKey>.Default);

    public static GrowOnlyCache<TKey, TValue> New(IEnumerable<KeyValuePair<TKey, TValue>> items, IEqualityComparer<TKey>? comparer = null)
    {
        var cache = New(comparer);
        foreach (var (key, value) in items)
            cache.GetOrAdd(ref cache, key, (k, v) => v, value);
        return cache;
    }

    protected GrowOnlyCache(IEqualityComparer<TKey> comparer)
    {
        Lock = new();
        Comparer = comparer;
    }

    protected GrowOnlyCache(GrowOnlyCache<TKey, TValue> source)
    {
        Lock = source.Lock;
        Comparer = source.Comparer;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public abstract bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value);
    public abstract TValue GetOrAdd(
        ref GrowOnlyCache<TKey, TValue> cache, TKey key, Func<TKey, TValue> valueFactory);
    public abstract TValue GetOrAdd<TState>(
        ref GrowOnlyCache<TKey, TValue> cache, TKey key, Func<TKey, TState, TValue> valueFactory, TState state);
    public abstract IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator();
}
