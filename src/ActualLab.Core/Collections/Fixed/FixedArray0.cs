namespace ActualLab.Collections.Fixed;

[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public readonly struct FixedArray0<T> : IEquatable<FixedArray0<T>>
{
    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Span<T>.Empty;
    }

    public ReadOnlySpan<T> ReadOnlySpan {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ReadOnlySpan<T>.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray0<T> New() => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedArray0<T> New(params ReadOnlySpan<T> source) => default;

    // Extra constructors are here just for the API similarity with other FixedArrayN

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray0(in ReadOnlySpan<T> source)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedArray0(T[] items)
    { }

    // Equality

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FixedArray0<T> other && Equals(other);
    public bool Equals(FixedArray0<T> other) => true;
    public override int GetHashCode() => 0;
    public static bool operator ==(FixedArray0<T> left, FixedArray0<T> right) => left.Equals(right);
    public static bool operator !=(FixedArray0<T> left, FixedArray0<T> right) => !left.Equals(right);
}
