namespace ActualLab.Text;

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
