#if NETSTANDARD2_0

// ReSharper disable once CheckNamespace
namespace System.Collections.Concurrent;

/// <summary>
/// Compatibility extension methods for concurrent collections on .NET Standard 2.0.
/// </summary>
public static class ConcurrentCollectionsCompatExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue GetOrAdd<TKey, TValue, TArg>(
        this ConcurrentDictionary<TKey, TValue> dict,
        TKey key,
        Func<TKey, TArg, TValue> valueFactory,
        TArg argument)
        => dict.GetOrAdd(key, k => valueFactory(k, argument));
}

#endif
