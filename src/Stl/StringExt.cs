using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Toolkit.HighPerformance;
using ActualLab.Internal;
using ActualLab.OS;

namespace Stl;

public static class StringExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_0
    public static bool IsNullOrEmpty(this string? source)
#else
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? source)
#endif
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

    // ReSharper disable once InconsistentNaming
    public static string GetMD5HashCode(this string input)
    {
        var inputBytes = Encoding.ASCII.GetBytes(input);
#pragma warning disable CA5351
#if NET5_0_OR_GREATER
        var hashBytes = MD5.HashData(inputBytes);
#else
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(inputBytes);
#endif
#pragma warning restore CA5351
#if NET5_0_OR_GREATER
        return Convert.ToHexString(hashBytes);
#elif NETSTANDARD2_0
        return BitConverter.ToString(hashBytes).Replace("-", "");
#else
        return BitConverter.ToString(hashBytes).Replace("-", "", StringComparison.Ordinal);
#endif
    }
}
