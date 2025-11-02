using System.Globalization;

namespace ActualLab.Diagnostics;

public static class CodeLocation
{
    private const string UnknownFile = "<UnknownFile>";
    private const string UnknownMember = "<UnknownMember>";

    private static readonly ConcurrentDictionary<string, string> Cache1 = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(string? File, string? Member, int Line), string> Cache3 = new();

    public static string Format(string? file)
    {
        if (file.IsNullOrEmpty())
            return UnknownFile;
        return Cache1.GetOrAdd(file,
            static x => {
                try {
                    return Path.GetFileName(x) ?? UnknownFile;
                }
                catch (Exception) {
                    return UnknownFile;
                }
            });
    }

    public static string Format(string? file, string? member, int line = 0)
        => Cache3.GetOrAdd((file, member, line), // Line is more selective and faster to compare
            static x => x.Line <= 0
                ? string.Concat(
                    x.Member.NullIfEmpty() ?? UnknownMember,
                    " @ ", Format(x.File))
                : string.Concat(
                    x.Member.NullIfEmpty() ?? UnknownMember,
                    " @ ", Format(x.File),
                    ":", x.Line.ToString(CultureInfo.InvariantCulture)));
}
