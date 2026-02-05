using System.Globalization;
using System.Text.RegularExpressions;

namespace ActualLab.Diagnostics;

/// <summary>
/// Provides methods for formatting source code locations (file, member, line)
/// into human-readable strings for diagnostics.
/// </summary>
public static partial class CodeLocation
{
    // We use a regex to extract the file name, coz e.g. on WASM Path.FileName() doesn't properly parse Windows paths
#if NET7_0_OR_GREATER
    [GeneratedRegex(@"[^\\/]+$")]
    private static partial Regex FileNameReFactory();
    private static readonly Regex FileNameRe = FileNameReFactory();
#else
    private static readonly Regex FileNameRe = new(@"[^\\/]+$", RegexOptions.Compiled);
#endif
    private const string UnknownFile = "<UnknownFile>";
    private const string UnknownMember = "<UnknownMember>";

    private static readonly ConcurrentDictionary<string, string> Cache1 = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(string? File, string? Member, int Line), string> Cache3 = new();


    public static string Format(string? file)
    {
        if (file.IsNullOrEmpty())
            return UnknownFile;
        return Cache1.GetOrAdd(file,
            static x => FileNameRe.Match(x) is { Success: true } match
                ? match.Value
                : UnknownFile);
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
