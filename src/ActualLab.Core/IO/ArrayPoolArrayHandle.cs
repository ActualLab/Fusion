using System.Buffers;

namespace ActualLab.IO;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public sealed class ArrayPoolArrayHandle<T>(ArrayPool<T> pool, T[] array, int length, bool mustClear)
    : IDisposable, IHasDisposeStatus
{
    private T[]? _array = array;
    private int _refCount;

    public readonly ArrayPool<T> Pool = pool;
    public T[]? Array => _array;
    public readonly int Length = length;
    public readonly bool MustClear = mustClear;
    public bool IsDisposed => Volatile.Read(ref _refCount) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        var array = Interlocked.Exchange(ref _array, null);
        if (!ReferenceEquals(array, null))
            Pool.Return(array, MustClear);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArrayPoolArrayRef<T> NewRef()
    {
        Interlocked.Increment(ref _refCount);
        return new ArrayPoolArrayRef<T>(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReleaseRef()
    {
        var refCount = Interlocked.Decrement(ref _refCount);
        if (refCount == 0)
            Dispose();
    }
}
