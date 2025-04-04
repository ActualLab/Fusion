namespace ActualLab.Text;

public static class StringExt
{
    // GetXxxHashCode

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetOrdinalHashCode(this string source)
#if !NETSTANDARD2_0
        => source.GetHashCode(StringComparison.Ordinal);
#else
        => source.AsSpan().GetXxHash3();
#endif

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
