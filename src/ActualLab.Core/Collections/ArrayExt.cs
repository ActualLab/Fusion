namespace ActualLab.Collections;

public static class ArrayExt
{
    public static T[] CloneArray<T>(this T[] source)
    {
#if NET5_0_OR_GREATER
        var result = GC.AllocateUninitializedArray<T>(source.Length);
#else
        var result = new T[source.Length];
#endif
        source.AsSpan().CopyTo(result);
        return result;
    }

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

    private sealed class SortKeyComparer<T, TSortKey>(
        Func<T, TSortKey> sortKeySelector,
        IComparer<TSortKey> comparer
        ) : IComparer<T>
    {
        public int Compare(T? x, T? y)
            => comparer.Compare(sortKeySelector(x!), sortKeySelector(y!));
    }
}
