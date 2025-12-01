using ActualLab.Internal;

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
    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static string Interpolate(this string source, params object[] args)
        => string.Format(ReflectionFormatProvider.Instance, source, args);
}
