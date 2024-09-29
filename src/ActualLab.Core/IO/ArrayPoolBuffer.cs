using System.Buffers;
using System.Diagnostics.Contracts;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;

namespace ActualLab.IO;

public sealed class ArrayPoolBuffer<T>(ArrayPool<T> pool, int initialCapacity) : IBuffer<T>, IMemoryOwner<T>
{
    private const int MinCapacity = 16;
    private const int DefaultInitialCapacity = 256;

    private T[] _array = pool.Rent(RoundCapacity(initialCapacity));
    private int _position;

    public bool MustClear { get; init; }
#if !NETSTANDARD2_0
        = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#else
        = true; // Not sure what's a better way to do this
#endif
    public T[] Array => _array;

    /// <inheritdoc/>
    Memory<T> IMemoryOwner<T>.Memory => MemoryMarshal.AsMemory(WrittenMemory);

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

    public ArrayPoolBuffer()
        : this(ArrayPool<T>.Shared, DefaultInitialCapacity)
    { }

    public ArrayPoolBuffer(ArrayPool<T> pool)
        : this(pool, DefaultInitialCapacity)
    { }

    public ArrayPoolBuffer(int initialCapacity)
        : this(ArrayPool<T>.Shared, initialCapacity)
    { }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _array, null!) is { } array)
            pool.Return(array, MustClear);
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

    public void Reset(int capacity)
    {
        _position = 0;
        ReplaceBuffer(capacity);
    }

    public void Reset(int capacity, int maxCapacity)
    {
        _position = 0;
        if (_array.Length > maxCapacity)
            ReplaceBuffer(capacity);
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
    {
        capacity = RoundCapacity(capacity);
#pragma warning disable CS8601 // Possible null reference assignment.
        pool.Resize(ref _array, capacity);
#pragma warning restore CS8601 // Possible null reference assignment.
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ReplaceBuffer(int capacity)
    {
        capacity = RoundCapacity(capacity);
        pool.Return(_array, MustClear);
        _array = pool.Rent(capacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundCapacity(int capacity)
        => Math.Max(MinCapacity, (int)Bits.GreaterOrEqualPowerOf2((ulong)capacity));
}
