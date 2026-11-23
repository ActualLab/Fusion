using ActualLab.Compression;
using ActualLab.Interception.Serialization;
using ActualLab.Rpc.Compression;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc;

/// <summary>
/// Defines a named RPC wire format with its argument and message serializer factories.
/// The built-in formats are in <see cref="RpcDefaultWireFormats"/>.
/// </summary>
public sealed class RpcWireFormat(
    string key,
    Func<ArgumentListSerializer> argumentSerializerFactory,
    Func<RpcPeer, RpcMessageSerializer> messageSerializerFactory,
    DataFormat dataFormat,
    RpcCompressionFormat? compressionFormat = null,
    RpcCompressionMode compressionMode = RpcCompressionMode.None)
{
    private readonly Lazy<ArgumentListSerializer> _argumentSerializerLazy = new(argumentSerializerFactory);

    public string Key { get; } = key;
    public ArgumentListSerializer ArgumentListSerializer => _argumentSerializerLazy.Value;
    public Func<ArgumentListSerializer> ArgumentSerializerFactory => argumentSerializerFactory;
    public Func<RpcPeer, RpcMessageSerializer> MessageSerializerFactory => messageSerializerFactory;
    // Text formats travel as text frames, so they can't be compressed
    public DataFormat DataFormat { get; } = dataFormat;
    public RpcCompressionFormat? CompressionFormat { get; }
        = compressionMode != RpcCompressionMode.None ? compressionFormat : null;
    public RpcCompressionMode CompressionMode { get; }
        = compressionFormat != null ? compressionMode : RpcCompressionMode.None;

    public override string ToString()
        => $"{GetType().GetName()}({Key})";

    // The "<key>-<suffix>" variant compressed with the codec in the given mode, e.g.
    // ("lz4", LZ4ByteCodec.Default) or ("lz4d1f", lz4WithDictionary, RpcCompressionMode.Full)
    public RpcWireFormat WithCompression(
        string suffix,
        ByteCodec codec,
        RpcCompressionMode compressionMode = RpcCompressionMode.ServerToClient,
        RpcCompressionOptions? options = null)
    {
        ValidateSuffix(suffix, codec, compressionMode);
        var compressionFormat = RpcCompressionFormat.Get(codec, options);
        return WithCompressionFormat($"{Key}-{suffix}", compressionFormat, compressionMode);
    }

    public RpcWireFormat WithCompressionFormat(
        string key, RpcCompressionFormat compressionFormat, RpcCompressionMode compressionMode)
    {
        if (DataFormat == DataFormat.Text)
            throw new InvalidOperationException($"Format '{Key}' is a text format, so it can't be compressed.");
        if (CompressionFormat is not null)
            throw new InvalidOperationException($"Format '{Key}' is already compressed.");

        return new(key, argumentSerializerFactory, messageSerializerFactory, DataFormat,
            compressionFormat, compressionMode);
    }

    // Internal methods

    // Peers agree on a format by its key alone, so the well-known "<codec>" / "<codec>f" suffixes
    // (e.g. "lz4" / "lz4f") must always mean the codec's default configuration in the same mode
    internal static void ValidateSuffix(string suffix, ByteCodec codec, RpcCompressionMode compressionMode)
    {
        if (suffix.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(suffix));
        if (compressionMode == RpcCompressionMode.None)
            throw new ArgumentOutOfRangeException(nameof(compressionMode));

        foreach (var name in (string[])["lz4", "zstd"]) {
            var expectedMode = string.Equals(suffix, name, StringComparison.Ordinal)
                ? RpcCompressionMode.ServerToClient
                : string.Equals(suffix, name + "f", StringComparison.Ordinal)
                    ? RpcCompressionMode.Full
                    : RpcCompressionMode.None;
            if (expectedMode == RpcCompressionMode.None)
                continue;

            if (!string.Equals(codec.DefaultName, name, StringComparison.Ordinal))
                throw new ArgumentOutOfRangeException(nameof(suffix),
                    $"Suffix '{suffix}' is reserved for the default '{name}' codec, but got {codec}.");
            if (compressionMode != expectedMode)
                throw new ArgumentOutOfRangeException(nameof(compressionMode),
                    $"Suffix '{suffix}' is reserved for RpcCompressionMode.{expectedMode}.");
        }
    }
}
