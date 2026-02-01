using System.Buffers;

namespace ActualLab.Collections.Internal;

public sealed class NonPoolingArrayPool<T> : ArrayPool<T>
    where T : unmanaged
{
    public static NonPoolingArrayPool<T> Instance { get; } = new();

    public override T[] Rent(int minimumLength)
#if NET5_0_OR_GREATER
        => GC.AllocateUninitializedArray<T>(minimumLength);
#else
        => new T[minimumLength];
#endif

    public override void Return(T[] array, bool clearArray = false)
    { }
}
