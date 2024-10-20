using ActualLab.Comparison;
using ActualLab.Internal;

namespace ActualLab.Rpc;

public readonly record struct LegacyName : ICanBeNone<LegacyName>
{
    public static LegacyName None => default;
    public static readonly IComparer<LegacyName> MaxVersionComparer = new MaxVersionRelationalComparer();
    private readonly Version? _maxVersion;

    public Symbol Name { get; init; }

    public Version MaxVersion {
        get => _maxVersion ?? VersionExt.MaxValue;
        init => _maxVersion = value;
    }

    public bool IsNone => ReferenceEquals(_maxVersion, null);

    public LegacyName(Symbol Name, Version MaxVersion)
    {
        this.Name = Name;
        this.MaxVersion = MaxVersion;
    }

    public override string ToString()
        => $"({MaxVersion.Format()}: {Name.Value})";

    public static LegacyName New(LegacyNameAttribute attribute, string suffix = "")
    {
        if (attribute.Name.IsNullOrEmpty())
            throw Errors.Constraint(
                $"{nameof(LegacyNameAttribute)}.{nameof(LegacyNameAttribute.Name)} cannot be empty.");
        return new LegacyName(attribute.Name + suffix, VersionExt.Parse(attribute.MaxVersion, true));
    }

    // Conversion

    public void Deconstruct(out Symbol name, out Version maxVersion)
    {
        name = Name;
        maxVersion = MaxVersion;
    }

    public void Deconstruct(out Symbol name, out Version maxVersion, out bool isNone)
    {
        name = Name;
        maxVersion = MaxVersion;
        isNone = IsNone;
    }

    public static implicit operator LegacyName(Version version)
        => new(Symbol.Empty, version);

    // Equality

    public bool Equals(LegacyName other)
        => IsNone ? other.IsNone
            : Name == other.Name && _maxVersion == other._maxVersion;

    public override int GetHashCode()
        => IsNone ? 0
            : HashCode.Combine(Name, _maxVersion);

    // Nested types

    private sealed class MaxVersionRelationalComparer : IComparer<LegacyName>
    {
        public int Compare(LegacyName x, LegacyName y)
            => x.MaxVersion.CompareTo(y.MaxVersion);
    }
}
