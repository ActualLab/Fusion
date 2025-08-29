using ActualLab.Internal;

namespace ActualLab.Comparison;

public static class VersionExt
{
    public static readonly Version MaxValue = new(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);

    public static Version Min(Version x, Version y)
        => x.CompareTo(y) <= 0 ? x : y;
    public static Version Max(Version x, Version y)
        => x.CompareTo(y) > 0 ? x : y;

    public static string Format(this Version? version)
        => ReferenceEquals(version, null) || version == MaxValue
            ? "<Inf>"
            : version.ToString();

    public static Version Parse(string s, bool useMaxValueIfEmpty = false)
        => s.IsNullOrEmpty()
            ? useMaxValueIfEmpty ? MaxValue : throw Errors.Format<Version>()
            : Version.Parse(s[0] == 'v' ? s[1..] : s);
}
