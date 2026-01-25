using System.Buffers;

namespace ActualLab.IO;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public sealed class ArrayPoolArrayHandle<T>(ArrayPool<T> pool, T[] array, int writtenCount, bool mustClear)
    : IDisposable, IHasDisposeStatus
{
    private T[]? _array = array;
    private int _refCount;

    public readonly ArrayPool<T> Pool = pool;
    public readonly int WrittenCount = writtenCount;
    public readonly bool MustClear = mustClear;

    public bool IsDisposed {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _refCount) == 0;
    }

    public T[] Array {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array ?? throw new ObjectDisposedException(nameof(ArrayPoolArrayHandle<>));
    }

    public ReadOnlyMemory<T> WrittenMemory {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Array.AsMemory(0, WrittenCount);
    }

    public ReadOnlySpan<T> WrittenSpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Array.AsSpan(0, WrittenCount);
    }

    public ArraySegment<T> WrittenArraySegment {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Array, 0, WrittenCount);
    }

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
