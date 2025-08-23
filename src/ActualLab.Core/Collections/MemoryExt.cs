namespace ActualLab.Collections;

public static class MemoryExt
{
    // TryGetUnderlyingArray

    public static T[]? TryGetUnderlyingArray<T>(this ReadOnlyMemory<T> items)
    {
#if !NETSTANDARD2_0
        if (MemoryMarshal.TryGetArray(items, out var segment)
            && segment.Array is { } array
            && array.Length == items.Length)
            return array;
#endif
        return null;
    }

    // AsMemoryUnsafe

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemoryUnsafe<T>(this ReadOnlyMemory<T> readOnlyMemory)
        => Unsafe.As<ReadOnlyMemory<T>, Memory<T>>(ref readOnlyMemory);
}
