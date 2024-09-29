using System.Diagnostics.CodeAnalysis;

namespace ActualLab;

public static class StringExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? source)
        => string.IsNullOrEmpty(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? NullIfEmpty(this string? source)
        => string.IsNullOrEmpty(source) ? null : source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? NullIfWhiteSpace(this string? source)
        => string.IsNullOrWhiteSpace(source) ? null : source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Interpolate(this string source, params object[] args)
        => string.Format(new ReflectionFormatProvider(), source, args);
}
