using ActualLab.Rpc;
using ActualLab.Rpc.Compression;
using ActualLab.Rpc.Serialization;

// ReSharper disable InconsistentNaming

namespace ActualLab.Serialization;

public static class RpcNerdbankSerializationFormat
{
    public static readonly RpcSerializationFormat NerdbankMessagePackV6 = new("nmsgpack6",
        () => new RpcByteArgumentSerializerV4(NerdbankMessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5(peer));
    public static readonly RpcSerializationFormat NerdbankMessagePackV6C = new("nmsgpack6c",
        () => new RpcByteArgumentSerializerV4(NerdbankMessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5Compact(peer));
    public static readonly RpcSerializationFormat NerdbankMessagePackV6_LZ4 = new("nmsgpack6-lz4",
        () => new RpcByteArgumentSerializerV4(NerdbankMessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5(peer),
        RpcCompressionFormat.LZ4, RpcCompressionMode.ServerToClient);
    public static readonly RpcSerializationFormat NerdbankMessagePackV6C_LZ4 = new("nmsgpack6c-lz4",
        () => new RpcByteArgumentSerializerV4(NerdbankMessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5Compact(peer),
        RpcCompressionFormat.LZ4, RpcCompressionMode.ServerToClient);
    public static readonly RpcSerializationFormat NerdbankMessagePackV6_LZ4F = new("nmsgpack6-lz4f",
        () => new RpcByteArgumentSerializerV4(NerdbankMessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5(peer),
        RpcCompressionFormat.LZ4, RpcCompressionMode.Full);
    public static readonly RpcSerializationFormat NerdbankMessagePackV6C_LZ4F = new("nmsgpack6c-lz4f",
        () => new RpcByteArgumentSerializerV4(NerdbankMessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5Compact(peer),
        RpcCompressionFormat.LZ4, RpcCompressionMode.Full);

    public static void Register()
    {
        var formats = RpcSerializationFormat.All;
        foreach (var format in (RpcSerializationFormat[])[
            NerdbankMessagePackV6, NerdbankMessagePackV6C,
            NerdbankMessagePackV6_LZ4, NerdbankMessagePackV6C_LZ4,
            NerdbankMessagePackV6_LZ4F, NerdbankMessagePackV6C_LZ4F,
        ]) {
            if (!formats.Any(x => string.Equals(x.Key, format.Key, StringComparison.Ordinal)))
                formats = formats.Add(format);
        }
        if (!ReferenceEquals(formats, RpcSerializationFormat.All)) {
            RpcSerializationFormat.All = formats;
            // Reset the resolver so it picks up the newly registered formats
            RpcSerializationFormatResolver.DefaultFormats = null!;
        }
    }
}
