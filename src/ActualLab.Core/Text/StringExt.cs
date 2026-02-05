namespace ActualLab.Text;

#pragma warning disable MA0021

/// <summary>
/// Extension methods for <see cref="string"/> providing hashing and suffix trimming.
/// </summary>
public static class StringExt
{
    // GetXxxHashCode

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetOrdinalHashCode(this string source)
#pragma warning disable CA1307 // string.GetHashCode() is faster than string.GetHashCode(StringComparison.Ordinal)
        => source.GetHashCode();
#pragma warning restore CA1307

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetXxHash3L(this string source)
        => source.AsSpan().GetXxHash3L();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetXxHash3(this string source)
        => source.AsSpan().GetXxHash3();

    // TrimSuffix

    public static string TrimSuffix(this string source, string suffix)
        => source.EndsWith(suffix, StringComparison.Ordinal)
            ? source.Substring(0, source.Length - suffix.Length)
            : source;

    public static string TrimSuffixes(this string source, params ReadOnlySpan<string> suffixes)
    {
        foreach (var suffix in suffixes) {
            var result = source.TrimSuffix(suffix);
            if (!ReferenceEquals(result, source))
                return result;
        }
        return source;
    }
}
