namespace ActualLab.Collections;

/// <summary>
/// Extension methods for arrays.
/// </summary>
public static class ArrayExt
{
    // Duplicate

    public static T[] Duplicate<T>(this T[] source)
    {
        var length = source.Length;
        if (length == 0)
            return source;

#if NET5_0_OR_GREATER
        var result = GC.AllocateUninitializedArray<T>(length);
#else
        var result = new T[length];
#endif
        source.AsSpan().CopyTo(result);
        return result;
    }

    // SortInPlace

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] SortInPlace<T>(this T[] array)
    {
        Array.Sort(array);
        return array;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] SortInPlace<T>(this T[] array, IComparer<T> comparer)
    {
        Array.Sort(array, comparer);
        return array;
    }

    public static T[] SortInPlace<T, TSortKey>(this T[] array, Func<T, TSortKey> sortKeySelector)
    {
        var comparer = new SortKeyComparer<T, TSortKey>(sortKeySelector, Comparer<TSortKey>.Default);
        Array.Sort(array, comparer);
        return array;
    }

    // Nested types

    /// <summary>
    /// Compares items by a projected sort key using a specified comparer.
    /// </summary>
    private sealed class SortKeyComparer<T, TSortKey>(
        Func<T, TSortKey> sortKeySelector,
        IComparer<TSortKey> comparer
        ) : IComparer<T>
    {
        public int Compare(T? x, T? y)
            => comparer.Compare(sortKeySelector(x!), sortKeySelector(y!));
    }
}
