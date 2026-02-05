using System.Text.RegularExpressions;

namespace ActualLab.Diagnostics;

/// <summary>
/// Utility methods for sanitizing metric and diagnostics names.
/// </summary>
public static partial class DiagnosticsExt
{
#if NET7_0_OR_GREATER
    [GeneratedRegex("[^A-Za-z0-9_\\-/\\.]+")]
    private static partial Regex InvalidMetricNameCharReFactory();

    private static readonly Regex InvalidMetricNameCharRe = InvalidMetricNameCharReFactory();
#else
    private static readonly Regex InvalidMetricNameCharRe = new("[^A-Za-z0-9_\\-/\\.]+", RegexOptions.Compiled);
#endif

    // Helpers

    public static string FixName(string name)
        => InvalidMetricNameCharRe.Replace(name, "_");
}
