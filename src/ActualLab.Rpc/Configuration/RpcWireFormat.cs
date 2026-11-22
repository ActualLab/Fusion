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

    // "-lz4" compresses server-to-client frames only, "-lz4f" compresses both directions
    public RpcWireFormat WithLZ4(RpcCompressionMode compressionMode = RpcCompressionMode.ServerToClient)
    {
        var suffix = compressionMode switch {
            RpcCompressionMode.ServerToClient => "-lz4",
            RpcCompressionMode.Full => "-lz4f",
            _ => throw new ArgumentOutOfRangeException(nameof(compressionMode)),
        };
        return WithCompression(Key + suffix, RpcCompressionFormat.LZ4, compressionMode);
    }

    public RpcWireFormat WithCompression(
        string key, RpcCompressionFormat compressionFormat, RpcCompressionMode compressionMode)
    {
        if (DataFormat == DataFormat.Text)
            throw new InvalidOperationException($"Format '{Key}' is a text format, so it can't be compressed.");
        if (CompressionFormat is not null)
            throw new InvalidOperationException($"Format '{Key}' is already compressed.");

        return new(key, argumentSerializerFactory, messageSerializerFactory, DataFormat,
            compressionFormat, compressionMode);
    }
}
