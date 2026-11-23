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

    // The "<key>-<suffix>" variant compressed with LZ4 in the given mode, starting from the dictionary
    // (made by LZ4ByteCompression), e.g. "lz4" for ServerToClient and "lz4f" for Full without one
    public RpcWireFormat WithLZ4(
        string suffix,
        object? dictionary = null,
        RpcCompressionMode compressionMode = RpcCompressionMode.ServerToClient)
    {
        ValidateLZ4Suffix(suffix, compressionMode);
        return WithCompression($"{Key}-{suffix}", RpcCompressionFormat.NewLZ4(suffix, dictionary), compressionMode);
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

    // Internal methods

    // Peers agree on a format by its key alone, so the well-known "lz4" / "lz4f" keys must always mean
    // the same compression mode
    internal static void ValidateLZ4Suffix(string suffix, RpcCompressionMode compressionMode)
    {
        if (suffix.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(suffix));
        if ((suffix is "lz4" && compressionMode != RpcCompressionMode.ServerToClient)
            || (suffix is "lz4f" && compressionMode != RpcCompressionMode.Full))
            throw new ArgumentOutOfRangeException(nameof(compressionMode),
                $"Suffix '{suffix}' is reserved for RpcCompressionMode."
                + (suffix is "lz4" ? nameof(RpcCompressionMode.ServerToClient) : nameof(RpcCompressionMode.Full))
                + ".");
    }
}
