using System.Buffers;
using ActualLab.Collections.Internal;

namespace ActualLab.Collections;

public static class ArrayOwner
{
    public static ArrayOwner<T> New<T>(ArrayPool<T> pool, T[] array, int length, bool mustClear)
        => mustClear
            ? new CleaningArrayOwner<T>(pool, array, length)
            : new ArrayOwner<T>(pool, array, length);
}

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public class ArrayOwner<T>(ArrayPool<T> pool, T[] array, int length)
    : IMemoryOwner<T>, IHasDisposeStatus
{
    // ReSharper disable once InconsistentNaming
    protected T[]? _array = array;

    public readonly ArrayPool<T> Pool = pool;
    public readonly int Length = length;

    public bool IsDisposed {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _array) is null;
    }

    public T[] Array {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array ?? throw new ObjectDisposedException(nameof(ArrayOwner<>));
    }

    public Memory<T> Memory {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Array.AsMemory(0, Length);
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Array.AsSpan(0, Length);
    }

    public ArraySegment<T> ArraySegment {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Array, 0, Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Dispose()
    {
        var array = Interlocked.Exchange(ref _array, null);
        if (!ReferenceEquals(array, null))
            Pool.Return(array);
    }
}
