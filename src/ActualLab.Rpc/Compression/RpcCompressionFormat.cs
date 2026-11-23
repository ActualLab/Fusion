using ActualLab.Compression;

namespace ActualLab.Rpc.Compression;

/// <summary>
/// A named frame compression codec pair plus the policy its compressor follows.
/// An <see cref="RpcWireFormat"/> names one of these, and its
/// <see cref="RpcCompressionMode"/> says which directions actually use it.
/// </summary>
public sealed class RpcCompressionFormat(
    Symbol id,
    Func<ByteCompressor> compressorFactory,
    Func<ByteDecompressor> decompressorFactory,
    RpcCompressionOptions? options = null)
{
    public static readonly RpcCompressionFormat LZ4 = New("lz4", LZ4ByteCompression.Default);

    // A dictionary (made by LZ4ByteCompression) needs an id of its own: peers that use plain "lz4" / "lz4f"
    // formats must not accept a format compressed against it
    public static RpcCompressionFormat NewLZ4(
        Symbol id, object? dictionary = null, RpcCompressionOptions? options = null)
    {
        if (dictionary is null && options is null && id.Value is "lz4" or "lz4f")
            return LZ4;
        if (dictionary is not null && id.Value is "lz4" or "lz4f")
            throw new ArgumentOutOfRangeException(nameof(id),
                $"A format with a dictionary needs an id other than '{id}'.");

        return New(id, LZ4ByteCompression.Default, dictionary, options);
    }

    // Every compressor and decompressor of the format starts from the same, shared dictionary
    // (one created by the compression). The id ends up in wire format keys, so peers using
    // a different dictionary must use a different id.
    public static RpcCompressionFormat New(
        Symbol id, ByteCompression compression, object? dictionary = null, RpcCompressionOptions? options = null)
    {
        if (dictionary is not null)
            compression.NewDecompressor(dictionary).Dispose(); // Fails now rather than on every connection

        return new(id,
            () => compression.NewCompressor(dictionary),
            () => compression.NewDecompressor(dictionary),
            options);
    }

    public Symbol Id { get; } = id;
    public Func<ByteCompressor> CompressorFactory { get; } = compressorFactory;
    public Func<ByteDecompressor> DecompressorFactory { get; } = decompressorFactory;
    public RpcCompressionOptions Options { get; } = options ?? RpcCompressionOptions.Default;

    public override string ToString()
        => $"{GetType().GetName()}({Id})";
}
