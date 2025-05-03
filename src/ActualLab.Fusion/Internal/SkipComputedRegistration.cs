namespace ActualLab.Fusion.Internal;

#pragma warning disable CS0169 // Field is never used

public readonly struct SkipComputedRegistration : IEquatable<SkipComputedRegistration>
{
    public static readonly SkipComputedRegistration Option = default!;

    // See https://github.com/dotnet/runtime/pull/107198
    [Obsolete("This member exists solely to make Mono AOT work. Don't use it!")]
    private readonly byte _dummyValue;

    // Equality
    public bool Equals(SkipComputedRegistration other) => true;
    public override bool Equals(object? obj) => obj is SkipComputedRegistration;
    public override int GetHashCode() => 0;
}
