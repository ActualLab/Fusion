namespace ActualLab.Api;

public static class ApiCollectionExt
{
    internal const int MaxToStringItems = 5;

    // ToApiArray

    public static Api.ApiArray<T> ToApiArray<T>(this T[] source, bool copy = false)
        => new(copy ? source.ToArray() : source);

    public static Api.ApiArray<T> ToApiArray<T>(this IEnumerable<T> source)
        => new(source);

    public static async Task<Api.ApiArray<TSource>> ToApiArrayAsync<TSource>(
        this IAsyncEnumerable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        var buffer = ArrayBuffer<TSource>.Lease(false);
        try {
            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                buffer.Add(item);
            return buffer.Count == 0 ? default : new Api.ApiArray<TSource>(buffer.ToArray());
        }
        finally {
            buffer.Release();
        }
    }

    // That's just a bit more efficient conversion than .Select().ToApiArray()
    public static Api.ApiArray<TResult> ToApiArray<TSource, TResult>(
        this IReadOnlyCollection<TSource> source,
        Func<TSource, TResult> selector)
    {
        var result = new TResult[source.Count];
        var i = 0;
        foreach (var item in source)
            result[i++] = selector(item);
        return new Api.ApiArray<TResult>(result);
    }

    // ToApiList

    public static ApiList<T> ToApiList<T>(this IEnumerable<T> source)
        => new(source);

    public static async Task<ApiList<TSource>> ToApiListAsync<TSource>(
        this IAsyncEnumerable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        var list = new ApiList<TSource>();
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            list.Add(item);
        return list;
    }

    // ToApiMap

    public static ApiMap<TKey, TValue> ToApiMap<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source)
        where TKey : notnull
        => new(source);

    public static ApiMap<TKey, TValue> ToApiMap<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> source,
        IEqualityComparer<TKey> comparer)
        where TKey : notnull
        => new(source, comparer);

    public static ApiMap<TKey, TValue> ToApiMap<TKey, TValue>(this IDictionary<TKey, TValue> source)
        where TKey : notnull
        => new(source);

    public static ApiMap<TKey, TValue> ToApiMap<TKey, TValue>(
        this IDictionary<TKey, TValue> source,
        IEqualityComparer<TKey> comparer)
        where TKey : notnull
        => new(source, comparer);

    public static ApiMap<TKey, TValue> ToApiMap<T, TKey, TValue>(
        this IEnumerable<T> source,
        Func<T, TKey> keySelector,
        Func<T, TValue> valueSelector,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
        => new(source.Select(x => KeyValuePair.Create(keySelector(x), valueSelector(x))), comparer);

    // ToApiSet

    public static ApiSet<T> ToApiSet<T>(this IEnumerable<T> source)
        => new(source);

    public static ApiSet<T> ToApiSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer)
        => new(source, comparer);
}
