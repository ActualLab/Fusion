namespace ActualLab.Fusion.Internal;

#pragma warning disable CS0169 // Field is never used

/// <summary>
/// A marker struct used as a generic argument to skip <see cref="ComputedRegistry"/> registration.
/// </summary>
public readonly struct SkipComputedRegistration : IEquatable<SkipComputedRegistration>
{
    public static readonly SkipComputedRegistration Option = default!;

    // See https://github.com/dotnet/runtime/pull/107198
    [Obsolete("This member exists solely to make Mono AOT work. Don't use it!")]
#pragma warning disable CA1823 // Unused field
    private readonly byte _dummyValue;
#pragma warning restore CA1823

    // Equality
    public bool Equals(SkipComputedRegistration other) => true;
    public override bool Equals(object? obj) => obj is SkipComputedRegistration;
    public override int GetHashCode() => 0;
    public static bool operator ==(SkipComputedRegistration left, SkipComputedRegistration right) => left.Equals(right);
    public static bool operator !=(SkipComputedRegistration left, SkipComputedRegistration right) => !left.Equals(right);
}
