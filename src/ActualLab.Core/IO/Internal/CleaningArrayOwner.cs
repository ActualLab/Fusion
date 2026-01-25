using System.Buffers;

namespace ActualLab.IO.Internal;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal sealed class CleaningArrayOwner<T>(ArrayPool<T> pool, T[] array, int length)
    : ArrayOwner<T>(pool, array, length)
{
#pragma warning disable CA2215
    public override void Dispose()
#pragma warning restore CA2215
    {
        var array = Interlocked.Exchange(ref _array, null);
        if (!ReferenceEquals(array, null))
            Pool.Return(array, clearArray: true);
    }
}
