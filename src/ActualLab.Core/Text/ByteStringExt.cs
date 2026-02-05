namespace ActualLab.Text;

/// <summary>
/// Extension methods for converting byte arrays and memory to <see cref="ByteString"/>.
/// </summary>
public static class ByteStringExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteString AsByteString(this byte[] bytes)
        => new(bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteString AsByteString(this Memory<byte> bytes)
        => new(bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteString AsByteString(this ReadOnlyMemory<byte> bytes)
        => new(bytes);
}
