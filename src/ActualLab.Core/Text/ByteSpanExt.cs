using System.IO.Hashing;

namespace ActualLab.Text;

/// <summary>
/// Extension methods for byte spans providing hex string conversion and XxHash3 hashing.
/// </summary>
public static class ByteSpanExt
{
    // ToHexString

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToHexString(this byte[] bytes, int maxLength = 16)
        => ToHexString((ReadOnlySpan<byte>)bytes, maxLength);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToHexString(this Span<byte> bytes, int maxLength = 16)
        => ToHexString((ReadOnlySpan<byte>)bytes, maxLength);

    public static string ToHexString(this ReadOnlySpan<byte> bytes, int maxLength = 16)
    {
        var trimmed = bytes.Length <= maxLength
            ? bytes
            : bytes[..maxLength];
        var result =
#if NET5_0_OR_GREATER1
            Convert.ToHexString(trimmed);
#else
            BitConverter.ToString(trimmed.ToArray());
#endif
        return trimmed.Length == bytes.Length
            ? result
            : result + "...";
    }

    // GetXxHash3

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetXxHash3L(this byte[] source)
        => XxHash3.HashToUInt64(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetXxHash3L(this Span<byte> source)
        => XxHash3.HashToUInt64(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetXxHash3L(this ReadOnlySpan<byte> source)
        => XxHash3.HashToUInt64(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetXxHash3(this byte[] source)
        => (int)XxHash3.HashToUInt64(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetXxHash3(this Span<byte> source)
        => unchecked((int)XxHash3.HashToUInt64(source));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetXxHash3(this ReadOnlySpan<byte> source)
        => (int)XxHash3.HashToUInt64(source);

    public static int GetPartialXxHash3(this ReadOnlySpan<byte> source)
    {
        var hash = source.Length <= 32
            ? source.GetXxHash3()
            : source[..16].GetXxHash3() + source[^16..].GetXxHash3();
        return (359 * source.Length) + hash;
    }
}
