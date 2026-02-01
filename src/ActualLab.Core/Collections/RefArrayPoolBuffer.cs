using System.Buffers;
using System.Diagnostics.Contracts;
using CommunityToolkit.HighPerformance;

namespace ActualLab.Collections;

// Ref struct version of ArrayPoolBuffer<T> - no allocations for its use.
// Use Release() instead of Dispose().
[StructLayout(LayoutKind.Auto)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public ref struct RefArrayPoolBuffer<T>(ArrayPool<T> pool, int initialCapacity, bool mustClear)
{
    private const int MinCapacity = 16;
    private const int DefaultInitialCapacity = 256;

    private T[] _array = pool.Rent(RoundCapacity(initialCapacity));
    private int _position = 0;

    public readonly ArrayPool<T> Pool = pool;
    public readonly bool MustClear = mustClear;

    public T[] Array {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array;
    }

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

    public readonly ReadOnlyMemory<T> WrittenMemory {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.AsMemory(0, _position);
    }

    public readonly ReadOnlySpan<T> WrittenSpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.AsSpan(0, _position);
    }

    public readonly ArraySegment<T> WrittenArraySegment {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_array, 0, _position);
    }

    public readonly int WrittenCount {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _position;
    }

    public readonly int Capacity {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.Length;
    }

    public readonly int FreeCapacity {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array.Length - _position;
    }

    // Constructors

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RefArrayPoolBuffer(ArrayPool<T> pool, int initialCapacity)
#if !NETSTANDARD2_0
        : this(pool, initialCapacity, RuntimeHelpers.IsReferenceOrContainsReferences<T>())
#else
        : this(pool, initialCapacity, true)
#endif
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RefArrayPoolBuffer(ArrayPool<T> pool, bool mustClear)
        : this(pool, DefaultInitialCapacity, mustClear)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RefArrayPoolBuffer(ArrayPool<T> pool)
        : this(pool, DefaultInitialCapacity)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RefArrayPoolBuffer(int initialCapacity, bool mustClear)
        : this(ArrayPool<T>.Shared, initialCapacity, mustClear)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RefArrayPoolBuffer(int initialCapacity)
        : this(ArrayPool<T>.Shared, initialCapacity)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RefArrayPoolBuffer(bool mustClear)
        : this(ArrayPool<T>.Shared, DefaultInitialCapacity, mustClear)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RefArrayPoolBuffer()
        : this(ArrayPool<T>.Shared, DefaultInitialCapacity)
    { }

    // Release (instead of Dispose)

    public void Release()
    {
        var array = _array;
        _array = null!;
        if (array is not null)
            Pool.Return(array, MustClear);
    }

    [Pure]
    public override readonly string ToString()
        => _array is char[] chars
            ? new string(chars, 0, _position)
            : $"RefArrayPoolBuffer<{typeof(T).Name}>[{_position}]";

    // IBufferWriter<T>-like members

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _array.AsMemory(_position);
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _array.AsSpan(_position);
    }

    public void Advance(int count)
    {
        if (count < 0 || _position + count > _array.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        _position += count;
    }

    public void Clear()
        => _array.AsSpan(0, _position).Clear();

    public void Reset()
        => _position = 0;

    public void Renew(int maxCapacity)
    {
        _position = 0;
        maxCapacity = RoundCapacity(maxCapacity);
        if (_array.Length <= maxCapacity)
            return;

        Pool.Return(_array, MustClear);
        _array = Pool.Rent(maxCapacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int sizeHint = 0)
    {
        var capacity = _position + Math.Max(1, sizeHint);
        if (capacity > _array.Length)
            ResizeBuffer(capacity);
    }

    // List-like members

    public int Count {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _position;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Position = value;
    }

    public T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable MA0012, CA2201
        get => index < _position ? _array[index] : throw new IndexOutOfRangeException();
#pragma warning restore MA0012, CA2201
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set {
#pragma warning disable MA0012, CA2201
            if (index >= _position)
                throw new IndexOutOfRangeException();
#pragma warning restore MA0012, CA2201
            _array[index] = value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<T>.Enumerator GetEnumerator() => WrittenSpan.GetEnumerator();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T[] ToArray() => WrittenSpan.ToArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        EnsureCapacity(1);
        _array[_position++] = item;
    }

    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items)
            Add(item);
    }

    public void AddRange(IReadOnlyCollection<T> items)
    {
        EnsureCapacity(items.Count);
        foreach (var item in items)
            _array[_position++] = item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddSpan(ReadOnlySpan<T> span)
    {
        EnsureCapacity(span.Length);
        span.CopyTo(_array.AsSpan(_position));
        _position += span.Length;
    }

    public void Insert(int index, T item)
    {
        EnsureCapacity(1);
        var copyLength = _position - index;
#pragma warning disable MA0012, CA2201
        if (copyLength < 0)
            throw new IndexOutOfRangeException();
#pragma warning restore MA0012, CA2201
        var span = _array.AsSpan(0, ++_position);
        var source = span.Slice(index, copyLength);
        var target = span[(index + 1)..];
        source.CopyTo(target);
        span[index] = item;
    }

    public void RemoveAt(int index)
    {
        var copyLength = _position - index - 1;
#pragma warning disable MA0012, CA2201
        if (copyLength < 0)
            throw new IndexOutOfRangeException();
#pragma warning restore MA0012, CA2201
        var span = _array.AsSpan(0, _position--);
        var source = span.Slice(index + 1, copyLength);
        var target = span[index..];
        source.CopyTo(target);
    }

    // Private methods

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ResizeBuffer(int capacity)
        => Pool.Resize(ref _array, RoundCapacity(capacity));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundCapacity(int capacity)
        => Math.Max(MinCapacity, (int)Bits.GreaterOrEqualPowerOf2((ulong)capacity));
}
