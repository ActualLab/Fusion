using ActualLab.Compression;

namespace ActualLab.Rpc.Compression;

/// <summary>
/// A named frame compression codec pair plus the policy its compressor follows.
/// An <see cref="RpcSerializationFormat"/> names one of these, and its
/// <see cref="RpcCompressionMode"/> says which directions actually use it.
/// </summary>
public sealed class RpcCompressionFormat(
    Symbol id,
    Func<ByteCompressor> compressorFactory,
    Func<ByteDecompressor> decompressorFactory,
    RpcCompressionOptions? options = null)
{
    public static readonly RpcCompressionFormat LZ4 = new("lz4",
        static () => new LZ4ByteCompressor(),
        static () => new LZ4ByteDecompressor());

    public Symbol Id { get; } = id;
    public Func<ByteCompressor> CompressorFactory { get; } = compressorFactory;
    public Func<ByteDecompressor> DecompressorFactory { get; } = decompressorFactory;
    public RpcCompressionOptions Options { get; } = options ?? RpcCompressionOptions.Default;

    public override string ToString()
        => $"{GetType().GetName()}({Id})";
}
