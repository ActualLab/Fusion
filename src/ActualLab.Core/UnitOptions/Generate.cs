namespace ActualLab;

#pragma warning disable CS0169 // Field is never used

// This type is used as an extra parameter of constructors to indicate newly generated Id required

/// <summary>
/// A unit-type constructor parameter indicating that a new identifier should be generated.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
public readonly struct Generate : IEquatable<Generate>
{
    public static readonly Generate Option = default!;

    // See https://github.com/dotnet/runtime/pull/107198
    [Obsolete("This member exists solely to make Mono AOT work. Don't use it!")]
    private readonly byte _dummyValue;

    // Equality
    public bool Equals(Generate other) => true;
    public override bool Equals(object? obj) => obj is Generate;
    public override int GetHashCode() => 0;
    public static bool operator ==(Generate left, Generate right) => left.Equals(right);
    public static bool operator !=(Generate left, Generate right) => !left.Equals(right);
}
