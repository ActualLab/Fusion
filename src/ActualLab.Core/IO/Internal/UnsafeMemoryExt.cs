namespace ActualLab.IO.Internal;

public static class UnsafeMemoryExt
{
    // AsMemoryUnsafe

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemoryUnsafe<T>(this ReadOnlyMemory<T> readOnlyMemory)
        => Unsafe.As<ReadOnlyMemory<T>, Memory<T>>(ref readOnlyMemory);
}
