namespace ActualLab.Collections;

public static class ConcurrentDictionaryExt
{
    // GetOrAdd with LazySlim

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue GetOrAdd<TKey, TValue>(
        this ConcurrentDictionary<TKey, LazySlim<TKey, TValue>> dictionary,
        TKey key, Func<TKey, TValue> factory)
        where TKey : notnull
        => dictionary.TryGetValue(key, out var value)
            ? value.Value
            : dictionary.GetOrAdd(key, LazySlim.New(key, factory)).Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue GetOrAdd<TKey, TState, TValue>(
        this ConcurrentDictionary<TKey, LazySlim<TKey, TState, TValue>> dictionary,
        TKey key, Func<TKey, TState, TValue> factory, TState state)
        where TKey : notnull
        => dictionary.TryGetValue(key, out var value)
            ? value.Value
            : dictionary.GetOrAdd(key, LazySlim.New(key, state, factory)).Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue GetOrAdd<TKey, TValue>(
        this ConcurrentDictionary<TKey, LazySlim<TValue>> dictionary,
        TKey key, Func<TValue> factory)
        where TKey : notnull
        => dictionary.TryGetValue(key, out var value)
            ? value.Value
            : dictionary.GetOrAdd(key, LazySlim.New(factory)).Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue GetOrAdd<TKey, TState, TValue>(
        this ConcurrentDictionary<TKey, LazySlim<TState, TValue>> dictionary,
        TKey key, Func<TState, TValue> factory, TState state)
        where TKey : notnull
        => dictionary.TryGetValue(key, out var value)
            ? value.Value
            : dictionary.GetOrAdd(key, LazySlim.New(state, factory)).Value;

    // Increment & Decrement

    public static int Increment<TKey>(this ConcurrentDictionary<TKey, int> dictionary, TKey key)
        where TKey : notnull
    {
        while (true) {
            if (dictionary.TryGetValue(key, out var value)) {
                var newValue = value + 1;
                if (dictionary.TryUpdate(key, newValue, value))
                    return newValue;
            }
            else {
                if (dictionary.TryAdd(key, 1))
                    return 1;
            }
        }
    }

    public static int Decrement<TKey>(this ConcurrentDictionary<TKey, int> dictionary, TKey key)
        where TKey : notnull
    {
        while (true) {
            var value = dictionary[key];
            if (value > 1) {
                var newValue = value - 1;
                if (dictionary.TryUpdate(key, newValue, value))
                    return newValue;
            }
            else {
                if (dictionary.TryRemove(key, value))
                    return 0;
            }
        }
    }

    // Handy TryRemove overload

    public static bool TryRemove<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key, TValue value)
        where TKey : notnull
        // Based on:
        // - https://devblogs.microsoft.com/pfxteam/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
        => ((ICollection<KeyValuePair<TKey, TValue>>) dictionary)
            .Remove(KeyValuePair.Create(key, value));
}
