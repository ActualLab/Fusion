namespace ActualLab.Collections;

/// <summary>
/// Extension methods for <see cref="ImmutableDictionary{TKey, TValue}"/>.
/// </summary>
public static class ImmutableDictionaryExt
{
    public static ImmutableDictionary<TKey, TValue> SetItems<TKey, TValue>(
        this ImmutableDictionary<TKey, TValue> source,
        params ReadOnlySpan<(TKey Key, TValue Value)> items)
        where TKey : notnull
    {
        foreach (var (key, value) in items)
            source = source.SetItem(key, value);
        return source;
    }
}
