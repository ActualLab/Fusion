using System.Buffers;
using System.Diagnostics.Contracts;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;

namespace ActualLab.IO;

public sealed class ArrayPoolBuffer<T>(ArrayPool<T> pool, int initialCapacity, bool mustClear)
    : IBuffer<T>, IMemoryOwner<T>
{
    private const int MinCapacity = 16;
    private const int DefaultInitialCapacity = 256;

    private T[] _array = pool.Rent(RoundCapacity(initialCapacity));
    private int _position;

    public readonly ArrayPool<T> Pool = pool;
    public T[] Array => _array;
    public readonly bool MustClear = mustClear;

    public int Position {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _position;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set {
            if (value < 0 || value > _array.Length)
                throw new ArgumentOutOfRangeException(nameof(value));

            _position = value;
        }
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<T> WrittenMemory {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.AsMemory(0, _position);
    }

    /// <inheritdoc/>
    public ReadOnlySpan<T> WrittenSpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.AsSpan(0, _position);
    }

    public ArraySegment<T> WrittenArraySegment {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_array, 0, _position);
    }

    /// <inheritdoc/>
    public int WrittenCount {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _position;
    }

    /// <inheritdoc/>
    public int Capacity {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.Length;
    }

    /// <inheritdoc/>
    public int FreeCapacity {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.Length - _position;
    }

    /// <inheritdoc/>
    Memory<T> IMemoryOwner<T>.Memory => MemoryMarshal.AsMemory(WrittenMemory);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayPoolBuffer<T> NewOrRenew(
        ref ArrayPoolBuffer<T>? buffer, int minCapacity, int maxCapacity)
    {
        if (buffer is null)
            return buffer = new ArrayPoolBuffer<T>(minCapacity);

        buffer.Renew(minCapacity, maxCapacity);
        return buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayPoolBuffer<T> NewOrRenew(
        ref ArrayPoolBuffer<T>? buffer, int minCapacity, int maxCapacity, bool mustClear)
    {
        if (buffer is null)
            return buffer = new ArrayPoolBuffer<T>(minCapacity, mustClear);

        buffer.Renew(minCapacity, maxCapacity);
        return buffer;
    }

    public ArrayPoolBuffer()
        : this(ArrayPool<T>.Shared, DefaultInitialCapacity)
    { }

    public ArrayPoolBuffer(bool mustClear)
        : this(ArrayPool<T>.Shared, DefaultInitialCapacity, mustClear)
    { }

    public ArrayPoolBuffer(ArrayPool<T> pool)
        : this(pool, DefaultInitialCapacity)
    { }

    public ArrayPoolBuffer(ArrayPool<T> pool, bool mustClear)
        : this(pool, DefaultInitialCapacity, mustClear)
    { }

    public ArrayPoolBuffer(int initialCapacity)
        : this(ArrayPool<T>.Shared, initialCapacity)
    { }

    public ArrayPoolBuffer(int initialCapacity, bool mustClear)
        : this(ArrayPool<T>.Shared, initialCapacity, mustClear)
    { }

    public ArrayPoolBuffer(ArrayPool<T> pool, int initialCapacity)
#if !NETSTANDARD2_0
        : this(pool, initialCapacity, RuntimeHelpers.IsReferenceOrContainsReferences<T>())
#else
        : this(pool, initialCapacity, true)
#endif
    { }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _array, null!) is { } array)
            Pool.Return(array, MustClear);
    }

    /// <inheritdoc/>
    [Pure]
    public override string ToString()
        => _array is char[] chars
            ? new string(chars, 0, _position) // See comments in MemoryOwner<T> about this
            : $"{GetType().GetName()}[{_position}]"; // Same representation used in Span<T>

    /// <inheritdoc/>
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _array.AsMemory(_position);
    }

    /// <inheritdoc/>
    public Span<T> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _array.AsSpan(_position);
    }

    /// <inheritdoc/>
    public void Advance(int count)
    {
        if (count < 0 || _position + count > _array.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        _position += count;
    }

    /// <inheritdoc/>
    public void Clear()
        => _array.AsSpan(0, _position).Clear();

    public void Reset()
        => _position = 0;


    public void Renew(int minCapacity, int maxCapacity)
    {
        _position = 0;
        if (_array.Length > maxCapacity)
            ReplaceBuffer(minCapacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int sizeHint = 0)
    {
        var capacity = _position + Math.Max(1, sizeHint);
        if (capacity > _array.Length)
            ResizeBuffer(capacity);
    }

    // Private methods

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ResizeBuffer(int capacity)
        => Pool.Resize(ref _array, RoundCapacity(capacity));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ReplaceBuffer(int capacity)
    {
        Pool.Return(_array, MustClear);
        _array = Pool.Rent(RoundCapacity(capacity));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public ArrayOwner<T> ToArrayOwnerAndReset(int capacity)
    {
        var result = ArrayOwner.New(Pool, _array, _position, MustClear);
        _array = Pool.Rent(RoundCapacity(capacity));
        _position = 0;
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundCapacity(int capacity)
        => Math.Max(MinCapacity, (int)Bits.GreaterOrEqualPowerOf2((ulong)capacity));
}
