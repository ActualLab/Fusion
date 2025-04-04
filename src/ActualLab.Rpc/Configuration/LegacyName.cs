using ActualLab.Comparison;
using ActualLab.Internal;

namespace ActualLab.Rpc;

public sealed record LegacyName(string Name, Version MaxVersion)
{
    public static readonly IComparer<LegacyName> MaxVersionComparer = new MaxVersionRelationalComparer();

    public override string ToString()
        => $"({MaxVersion.Format()}: {Name})";

    public static LegacyName New(LegacyNameAttribute attribute, string suffix = "")
    {
        if (attribute.Name.IsNullOrEmpty())
            throw Errors.Constraint(
                $"{nameof(LegacyNameAttribute)}.{nameof(LegacyNameAttribute.Name)} cannot be empty.");
        return new LegacyName(attribute.Name + suffix, VersionExt.Parse(attribute.MaxVersion, true));
    }

    // Equality

    public bool Equals(LegacyName? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;

        return string.Equals(Name, other.Name, StringComparison.Ordinal) && Equals(MaxVersion, other.MaxVersion);
    }

    public override int GetHashCode()
        => HashCode.Combine(Name, MaxVersion);

    // Nested types

    private sealed class MaxVersionRelationalComparer : IComparer<LegacyName>
    {
        public int Compare(LegacyName? x, LegacyName? y)
            => (x?.MaxVersion ?? VersionExt.MaxValue).CompareTo(y?.MaxVersion ?? VersionExt.MaxValue);
    }
}
