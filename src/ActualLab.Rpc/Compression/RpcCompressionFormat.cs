using ActualLab.Compression;

namespace ActualLab.Rpc.Compression;

/// <summary>
/// A named, configured frame compression codec (<see cref="ByteCodec"/>) plus the policy its
/// encoder follows. An <see cref="RpcWireFormat"/> names one of these, and its
/// <see cref="RpcCompressionMode"/> says which directions actually use it.
/// </summary>
public sealed class RpcCompressionFormat(
    Symbol id,
    ByteCodec codec,
    RpcCompressionOptions? options = null)
{
    public static readonly RpcCompressionFormat LZ4 = new("lz4", LZ4ByteCodec.Default);

    public Symbol Id { get; } = id;
    public ByteCodec Codec { get; } = codec;
    public RpcCompressionOptions Options { get; } = options ?? RpcCompressionOptions.Default;

    public override string ToString()
        => $"{GetType().GetName()}({Id}, {Codec})";

    // Internal methods

    // Formats of a default codec configuration share the static instance
    internal static RpcCompressionFormat Get(Symbol id, ByteCodec codec, RpcCompressionOptions? options)
    {
        if (options is null) {
            if (ReferenceEquals(codec, LZ4ByteCodec.Default))
                return LZ4;
        }
        return new(id, codec, options);
    }
}
