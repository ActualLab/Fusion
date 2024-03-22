using System.Reflection.Emit;

namespace ActualLab.Collections;

public static class ConcurrentDictionaryExt
{
    public static int GetCapacity<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> source)
        where TKey : notnull
        => Cache<TKey, TValue>.CapacityReader(source);

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

    // Nested types

    private static class Cache<TKey, TValue>
        where TKey : notnull
    {
        public static readonly Func<ConcurrentDictionary<TKey, TValue>, int> CapacityReader;

        static Cache()
        {
#if !NETSTANDARD2_0
            var fTablesName = "_tables";
            var fBucketsName = "_buckets";
#else
            var fTablesName = "m_tables";
            var fBucketsName = "m_buckets";
#endif
            var fTables = typeof(ConcurrentDictionary<TKey, TValue>)
                .GetField(fTablesName, BindingFlags.Instance | BindingFlags.NonPublic)!;
#pragma warning disable IL2075
            var fBuckets = fTables.FieldType
                .GetField(fBucketsName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            var pLength = fBuckets.FieldType
                .GetProperty("Length", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
#pragma warning restore IL2075

            var m = new DynamicMethod("_CapacityReader",
                typeof(int), [typeof(ConcurrentDictionary<TKey, TValue>)],
                true);
            var il = m.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, fTables);
            il.Emit(OpCodes.Ldfld, fBuckets);
            il.Emit(OpCodes.Callvirt, pLength.GetMethod!);
            il.Emit(OpCodes.Ret);
            CapacityReader = (Func<ConcurrentDictionary<TKey, TValue>, int>)m.CreateDelegate(typeof(Func<ConcurrentDictionary<TKey, TValue>, int>));
        }
    }
}
