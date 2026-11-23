using ActualLab.Compression;

namespace ActualLab.Rpc.Compression;

/// <summary>
/// A configured frame compression codec (<see cref="ByteCodec"/>) plus the policy its encoder
/// follows. An <see cref="RpcWireFormat"/> refers to one of these, and its
/// <see cref="RpcCompressionMode"/> says which directions actually use it.
/// </summary>
public sealed class RpcCompressionFormat
{
    // Not validated: creating a codec's contexts in a type initializer would make this type unusable
    // where the codec isn't supported (e.g. Zstandard on Blazor WASM), even for LZ4 formats
    public static readonly RpcCompressionFormat LZ4 = new(LZ4ByteCodec.Default, null, mustValidate: false);
#if NET11_0_OR_GREATER
    public static readonly RpcCompressionFormat Zstd = new(ZstdByteCodec.Default, null, mustValidate: false);
#endif

    public ByteCodec Codec { get; }
    public RpcCompressionOptions Options { get; }

    public RpcCompressionFormat(ByteCodec codec, RpcCompressionOptions? options = null)
        : this(codec, options, mustValidate: true)
    { }

    private RpcCompressionFormat(ByteCodec codec, RpcCompressionOptions? options, bool mustValidate)
    {
        if (mustValidate) {
            // Fails here rather than on every connection; this also prepares the codec's dictionary
            codec.NewEncoder().Dispose();
            codec.NewDecoder().Dispose();
        }
        Codec = codec;
        Options = options ?? RpcCompressionOptions.Default;
    }

    public override string ToString()
        => $"{GetType().GetName()}({Codec}, {Options.ToShortString()})";

    // Internal methods

    // Formats of a default codec configuration share the static instance
    internal static RpcCompressionFormat Get(ByteCodec codec, RpcCompressionOptions? options)
    {
        if (options is null) {
            if (ReferenceEquals(codec, LZ4ByteCodec.Default))
                return LZ4;
#if NET11_0_OR_GREATER
            if (ReferenceEquals(codec, ZstdByteCodec.Default))
                return Zstd;
#endif
        }
        return new(codec, options);
    }
}
