using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Collections.Fixed;

#pragma warning disable CS0169 // Field is never used
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray1<T> : IEquatable<FixedArray1<T>>
{
#if !NETSTANDARD2_0
    private T _item0;

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray2<T> : IEquatable<FixedArray2<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray3<T> : IEquatable<FixedArray3<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray4<T> : IEquatable<FixedArray4<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public struct FixedArray5<T> : IEquatable<FixedArray5<T>>
{
#if !NETSTANDARD2_0
    private T _item0;
    private T _item1;
    private T _item2;
    private T _item3;
    private T _item4;

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}

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
#if NET6_0_OR_GREATER
        => Span.SequenceEqual(other.Span);
#else
    {
        var span = Span;
        var otherSpan = other.Span;
        if (span.Length != otherSpan.Length)
            return false;

        for (var i = 0; i < span.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(span[i], otherSpan[i]))
                return false;

        return true;
    }
#endif

    public override int GetHashCode()
    {
        var hashCode = 0;
        foreach (var item in Span)
            hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
        return hashCode;
    }
}
