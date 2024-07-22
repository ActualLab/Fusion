namespace ActualLab.Serialization;

public static class ByteSpanExt
{
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
}
