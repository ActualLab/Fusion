namespace ActualLab.Collections.Fixed;

#pragma warning disable CS0169 // Field is never used
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

/// <summary>
/// A fixed-size inline array of 1 element, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray1<T> : IEquatable<FixedArray1<T>>
{
#if !NETSTANDARD2_0
    private T _item0;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 1);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray1<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray1<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray1(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray1(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray1<T> New() => new(new T[1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray1<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray1(in ReadOnlySpan<T> source)
    {
        _items = new T[1];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray1(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray1<T> other && Equals(other);

    public bool Equals(FixedArray1<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray1<T> left, FixedArray1<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray1<T> left, FixedArray1<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 2 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray2<T> : IEquatable<FixedArray2<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 2);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray2<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray2<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray2(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray2(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray2<T> New() => new(new T[2]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray2<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray2(in ReadOnlySpan<T> source)
    {
        _items = new T[2];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray2(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray2<T> other && Equals(other);

    public bool Equals(FixedArray2<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray2<T> left, FixedArray2<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray2<T> left, FixedArray2<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 3 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray3<T> : IEquatable<FixedArray3<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 3);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray3<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray3<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray3(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray3(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray3<T> New() => new(new T[3]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray3<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray3(in ReadOnlySpan<T> source)
    {
        _items = new T[3];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray3(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray3<T> other && Equals(other);

    public bool Equals(FixedArray3<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray3<T> left, FixedArray3<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray3<T> left, FixedArray3<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 4 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray4<T> : IEquatable<FixedArray4<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 4);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray4<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray4<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray4(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray4(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[3];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray4<T> New() => new(new T[4]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray4<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray4(in ReadOnlySpan<T> source)
    {
        _items = new T[4];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray4(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray4<T> other && Equals(other);

    public bool Equals(FixedArray4<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            && comparer.Equals(_item3, other._item3)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item3?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray4<T> left, FixedArray4<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray4<T> left, FixedArray4<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 5 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray5<T> : IEquatable<FixedArray5<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;
    private T _item4;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 5);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray5<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray5<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray5(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray5(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[3];
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[4];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray5<T> New() => new(new T[5]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray5<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray5(in ReadOnlySpan<T> source)
    {
        _items = new T[5];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray5(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray5<T> other && Equals(other);

    public bool Equals(FixedArray5<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            && comparer.Equals(_item3, other._item3)
            && comparer.Equals(_item4, other._item4)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item3?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item4?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray5<T> left, FixedArray5<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray5<T> left, FixedArray5<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 6 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray6<T> : IEquatable<FixedArray6<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;
    private T _item4;
    private T _item5;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 6);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 6);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray6<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray6<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray6(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray6(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[3];
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[4];
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[5];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray6<T> New() => new(new T[6]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray6<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray6(in ReadOnlySpan<T> source)
    {
        _items = new T[6];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray6(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray6<T> other && Equals(other);

    public bool Equals(FixedArray6<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            && comparer.Equals(_item3, other._item3)
            && comparer.Equals(_item4, other._item4)
            && comparer.Equals(_item5, other._item5)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item3?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item4?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item5?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray6<T> left, FixedArray6<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray6<T> left, FixedArray6<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 7 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray7<T> : IEquatable<FixedArray7<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;
    private T _item4;
    private T _item5;
    private T _item6;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 7);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 7);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray7<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray7<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray7(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray7(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[3];
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[4];
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[5];
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[6];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray7<T> New() => new(new T[7]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray7<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray7(in ReadOnlySpan<T> source)
    {
        _items = new T[7];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray7(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray7<T> other && Equals(other);

    public bool Equals(FixedArray7<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            && comparer.Equals(_item3, other._item3)
            && comparer.Equals(_item4, other._item4)
            && comparer.Equals(_item5, other._item5)
            && comparer.Equals(_item6, other._item6)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item3?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item4?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item5?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item6?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray7<T> left, FixedArray7<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray7<T> left, FixedArray7<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 8 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray8<T> : IEquatable<FixedArray8<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;
    private T _item4;
    private T _item5;
    private T _item6;
    private T _item7;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 8);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray8<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray8<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray8(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray8(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[3];
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[4];
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[5];
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[6];
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[7];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray8<T> New() => new(new T[8]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray8<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray8(in ReadOnlySpan<T> source)
    {
        _items = new T[8];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray8(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray8<T> other && Equals(other);

    public bool Equals(FixedArray8<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            && comparer.Equals(_item3, other._item3)
            && comparer.Equals(_item4, other._item4)
            && comparer.Equals(_item5, other._item5)
            && comparer.Equals(_item6, other._item6)
            && comparer.Equals(_item7, other._item7)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item3?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item4?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item5?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item6?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item7?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray8<T> left, FixedArray8<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray8<T> left, FixedArray8<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 9 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray9<T> : IEquatable<FixedArray9<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;
    private T _item4;
    private T _item5;
    private T _item6;
    private T _item7;
    private T _item8;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item8;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 9);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 9);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray9<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray9<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray9(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray9(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[3];
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[4];
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[5];
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[6];
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[7];
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[8];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray9<T> New() => new(new T[9]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray9<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray9(in ReadOnlySpan<T> source)
    {
        _items = new T[9];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray9(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray9<T> other && Equals(other);

    public bool Equals(FixedArray9<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            && comparer.Equals(_item3, other._item3)
            && comparer.Equals(_item4, other._item4)
            && comparer.Equals(_item5, other._item5)
            && comparer.Equals(_item6, other._item6)
            && comparer.Equals(_item7, other._item7)
            && comparer.Equals(_item8, other._item8)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item3?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item4?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item5?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item6?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item7?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item8?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray9<T> left, FixedArray9<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray9<T> left, FixedArray9<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 10 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray10<T> : IEquatable<FixedArray10<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;
    private T _item4;
    private T _item5;
    private T _item6;
    private T _item7;
    private T _item8;
    private T _item9;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item8;
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item9;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 10);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 10);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray10<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray10<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray10(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray10(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[3];
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[4];
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[5];
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[6];
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[7];
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[8];
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[9];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray10<T> New() => new(new T[10]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray10<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray10(in ReadOnlySpan<T> source)
    {
        _items = new T[10];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray10(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray10<T> other && Equals(other);

    public bool Equals(FixedArray10<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            && comparer.Equals(_item3, other._item3)
            && comparer.Equals(_item4, other._item4)
            && comparer.Equals(_item5, other._item5)
            && comparer.Equals(_item6, other._item6)
            && comparer.Equals(_item7, other._item7)
            && comparer.Equals(_item8, other._item8)
            && comparer.Equals(_item9, other._item9)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item3?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item4?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item5?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item6?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item7?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item8?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item9?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray10<T> left, FixedArray10<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray10<T> left, FixedArray10<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 11 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray11<T> : IEquatable<FixedArray11<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;
    private T _item4;
    private T _item5;
    private T _item6;
    private T _item7;
    private T _item8;
    private T _item9;
    private T _item10;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item8;
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item9;
    }
    public T Item10 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item10;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 11);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 11);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray11<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray11<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray11(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray11(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[3];
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[4];
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[5];
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[6];
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[7];
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[8];
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[9];
    }
    public T Item10 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[10];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray11<T> New() => new(new T[11]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray11<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray11(in ReadOnlySpan<T> source)
    {
        _items = new T[11];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray11(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray11<T> other && Equals(other);

    public bool Equals(FixedArray11<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            && comparer.Equals(_item3, other._item3)
            && comparer.Equals(_item4, other._item4)
            && comparer.Equals(_item5, other._item5)
            && comparer.Equals(_item6, other._item6)
            && comparer.Equals(_item7, other._item7)
            && comparer.Equals(_item8, other._item8)
            && comparer.Equals(_item9, other._item9)
            && comparer.Equals(_item10, other._item10)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item3?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item4?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item5?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item6?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item7?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item8?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item9?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item10?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray11<T> left, FixedArray11<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray11<T> left, FixedArray11<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 12 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray12<T> : IEquatable<FixedArray12<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;
    private T _item4;
    private T _item5;
    private T _item6;
    private T _item7;
    private T _item8;
    private T _item9;
    private T _item10;
    private T _item11;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item8;
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item9;
    }
    public T Item10 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item10;
    }
    public T Item11 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item11;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 12);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 12);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray12<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray12<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray12(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray12(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[3];
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[4];
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[5];
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[6];
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[7];
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[8];
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[9];
    }
    public T Item10 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[10];
    }
    public T Item11 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[11];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray12<T> New() => new(new T[12]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray12<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray12(in ReadOnlySpan<T> source)
    {
        _items = new T[12];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray12(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray12<T> other && Equals(other);

    public bool Equals(FixedArray12<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            && comparer.Equals(_item3, other._item3)
            && comparer.Equals(_item4, other._item4)
            && comparer.Equals(_item5, other._item5)
            && comparer.Equals(_item6, other._item6)
            && comparer.Equals(_item7, other._item7)
            && comparer.Equals(_item8, other._item8)
            && comparer.Equals(_item9, other._item9)
            && comparer.Equals(_item10, other._item10)
            && comparer.Equals(_item11, other._item11)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item3?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item4?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item5?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item6?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item7?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item8?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item9?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item10?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item11?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray12<T> left, FixedArray12<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray12<T> left, FixedArray12<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 13 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray13<T> : IEquatable<FixedArray13<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;
    private T _item4;
    private T _item5;
    private T _item6;
    private T _item7;
    private T _item8;
    private T _item9;
    private T _item10;
    private T _item11;
    private T _item12;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item8;
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item9;
    }
    public T Item10 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item10;
    }
    public T Item11 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item11;
    }
    public T Item12 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item12;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 13);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 13);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray13<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray13<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray13(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray13(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[3];
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[4];
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[5];
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[6];
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[7];
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[8];
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[9];
    }
    public T Item10 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[10];
    }
    public T Item11 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[11];
    }
    public T Item12 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[12];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray13<T> New() => new(new T[13]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray13<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray13(in ReadOnlySpan<T> source)
    {
        _items = new T[13];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray13(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray13<T> other && Equals(other);

    public bool Equals(FixedArray13<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            && comparer.Equals(_item3, other._item3)
            && comparer.Equals(_item4, other._item4)
            && comparer.Equals(_item5, other._item5)
            && comparer.Equals(_item6, other._item6)
            && comparer.Equals(_item7, other._item7)
            && comparer.Equals(_item8, other._item8)
            && comparer.Equals(_item9, other._item9)
            && comparer.Equals(_item10, other._item10)
            && comparer.Equals(_item11, other._item11)
            && comparer.Equals(_item12, other._item12)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item3?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item4?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item5?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item6?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item7?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item8?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item9?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item10?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item11?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item12?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray13<T> left, FixedArray13<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray13<T> left, FixedArray13<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 14 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray14<T> : IEquatable<FixedArray14<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;
    private T _item4;
    private T _item5;
    private T _item6;
    private T _item7;
    private T _item8;
    private T _item9;
    private T _item10;
    private T _item11;
    private T _item12;
    private T _item13;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item8;
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item9;
    }
    public T Item10 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item10;
    }
    public T Item11 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item11;
    }
    public T Item12 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item12;
    }
    public T Item13 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item13;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 14);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 14);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray14<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray14<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray14(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray14(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[3];
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[4];
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[5];
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[6];
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[7];
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[8];
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[9];
    }
    public T Item10 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[10];
    }
    public T Item11 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[11];
    }
    public T Item12 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[12];
    }
    public T Item13 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[13];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray14<T> New() => new(new T[14]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray14<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray14(in ReadOnlySpan<T> source)
    {
        _items = new T[14];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray14(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray14<T> other && Equals(other);

    public bool Equals(FixedArray14<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            && comparer.Equals(_item3, other._item3)
            && comparer.Equals(_item4, other._item4)
            && comparer.Equals(_item5, other._item5)
            && comparer.Equals(_item6, other._item6)
            && comparer.Equals(_item7, other._item7)
            && comparer.Equals(_item8, other._item8)
            && comparer.Equals(_item9, other._item9)
            && comparer.Equals(_item10, other._item10)
            && comparer.Equals(_item11, other._item11)
            && comparer.Equals(_item12, other._item12)
            && comparer.Equals(_item13, other._item13)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item3?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item4?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item5?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item6?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item7?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item8?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item9?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item10?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item11?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item12?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item13?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray14<T> left, FixedArray14<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray14<T> left, FixedArray14<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 15 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray15<T> : IEquatable<FixedArray15<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;
    private T _item4;
    private T _item5;
    private T _item6;
    private T _item7;
    private T _item8;
    private T _item9;
    private T _item10;
    private T _item11;
    private T _item12;
    private T _item13;
    private T _item14;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item8;
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item9;
    }
    public T Item10 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item10;
    }
    public T Item11 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item11;
    }
    public T Item12 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item12;
    }
    public T Item13 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item13;
    }
    public T Item14 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item14;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 15);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 15);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray15<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray15<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray15(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray15(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[3];
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[4];
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[5];
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[6];
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[7];
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[8];
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[9];
    }
    public T Item10 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[10];
    }
    public T Item11 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[11];
    }
    public T Item12 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[12];
    }
    public T Item13 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[13];
    }
    public T Item14 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[14];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray15<T> New() => new(new T[15]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray15<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray15(in ReadOnlySpan<T> source)
    {
        _items = new T[15];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray15(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray15<T> other && Equals(other);

    public bool Equals(FixedArray15<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            && comparer.Equals(_item3, other._item3)
            && comparer.Equals(_item4, other._item4)
            && comparer.Equals(_item5, other._item5)
            && comparer.Equals(_item6, other._item6)
            && comparer.Equals(_item7, other._item7)
            && comparer.Equals(_item8, other._item8)
            && comparer.Equals(_item9, other._item9)
            && comparer.Equals(_item10, other._item10)
            && comparer.Equals(_item11, other._item11)
            && comparer.Equals(_item12, other._item12)
            && comparer.Equals(_item13, other._item13)
            && comparer.Equals(_item14, other._item14)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item3?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item4?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item5?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item6?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item7?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item8?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item9?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item10?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item11?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item12?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item13?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item14?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray15<T> left, FixedArray15<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray15<T> left, FixedArray15<T> right) => !left.Equals(right);
}

/// <summary>
/// A fixed-size inline array of 16 elements, stored on the stack via sequential layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray16<T> : IEquatable<FixedArray16<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;
    private T _item4;
    private T _item5;
    private T _item6;
    private T _item7;
    private T _item8;
    private T _item9;
    private T _item10;
    private T _item11;
    private T _item12;
    private T _item13;
    private T _item14;
    private T _item15;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item8;
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item9;
    }
    public T Item10 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item10;
    }
    public T Item11 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item11;
    }
    public T Item12 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item12;
    }
    public T Item13 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item13;
    }
    public T Item14 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item14;
    }
    public T Item15 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item15;
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _item0, 16);
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref _item0, 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray16<T> New() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray16<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray16(in ReadOnlySpan<T> source)
        => source.CopyTo(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray16(T[] items)
        => items.CopyTo(Span);
#else
    private readonly T[] _items;

    public T Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[0];
    }
    public T Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[1];
    }
    public T Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[2];
    }
    public T Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[3];
    }
    public T Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[4];
    }
    public T Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[5];
    }
    public T Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[6];
    }
    public T Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[7];
    }
    public T Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[8];
    }
    public T Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[9];
    }
    public T Item10 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[10];
    }
    public T Item11 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[11];
    }
    public T Item12 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[12];
    }
    public T Item13 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[13];
    }
    public T Item14 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[14];
    }
    public T Item15 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items[15];
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray16<T> New() => new(new T[16]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray16<T> New(params ReadOnlySpan<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray16(in ReadOnlySpan<T> source)
    {
        _items = new T[16];
        source.CopyTo(_items);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray16(T[] items)
        => _items = items;
#endif

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray16<T> other && Equals(other);

    public bool Equals(FixedArray16<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
#if !NETSTANDARD2_0
        return
            comparer.Equals(_item0, other._item0)
            && comparer.Equals(_item1, other._item1)
            && comparer.Equals(_item2, other._item2)
            && comparer.Equals(_item3, other._item3)
            && comparer.Equals(_item4, other._item4)
            && comparer.Equals(_item5, other._item5)
            && comparer.Equals(_item6, other._item6)
            && comparer.Equals(_item7, other._item7)
            && comparer.Equals(_item8, other._item8)
            && comparer.Equals(_item9, other._item9)
            && comparer.Equals(_item10, other._item10)
            && comparer.Equals(_item11, other._item11)
            && comparer.Equals(_item12, other._item12)
            && comparer.Equals(_item13, other._item13)
            && comparer.Equals(_item14, other._item14)
            && comparer.Equals(_item15, other._item15)
            ;
#else
        var otherItems = other._items;
        for (var i = 0; i < _items.Length; i++)
            if (!comparer.Equals(_items[i], otherItems[i]))
                return false;

        return true;
#endif
    }

    public override int GetHashCode()
    {
#if !NETSTANDARD2_0
        var hashCode = _item0?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item1?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item2?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item3?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item4?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item5?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item6?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item7?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item8?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item9?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item10?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item11?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item12?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item13?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item14?.GetHashCode() ?? 0;
        hashCode = (359 * hashCode) + _item15?.GetHashCode() ?? 0;
#else
        var hashCode = 0;
        foreach (var item in _items)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
#endif
        return hashCode;
    }

    public static bool operator ==(FixedArray16<T> left, FixedArray16<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray16<T> left, FixedArray16<T> right) => !left.Equals(right);
}
