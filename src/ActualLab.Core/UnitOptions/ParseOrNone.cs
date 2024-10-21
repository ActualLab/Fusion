namespace ActualLab;

#pragma warning disable CS0169 // Field is never used

// This type is used as an extra parameter of constructors to indicate no validation is required
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public readonly struct ParseOrNone : IEquatable<ParseOrNone>
{
    public static readonly ParseOrNone Option = default!;

    // See https://github.com/dotnet/runtime/pull/107198
    [Obsolete("This member exists solely to make Mono AOT work. Don't use it!")]
    private readonly byte _dummyValue;

    // Equality
    public bool Equals(ParseOrNone other) => true;
    public override bool Equals(object? obj) => obj is ParseOrNone;
    public override int GetHashCode() => 0;
}
