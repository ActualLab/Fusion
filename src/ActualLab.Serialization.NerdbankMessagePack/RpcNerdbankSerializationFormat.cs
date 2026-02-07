using ActualLab.Rpc;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Serialization;

public static class RpcNerdbankSerializationFormat
{
    public static readonly RpcSerializationFormat NerdbankMessagePackV6 = new("nmsgpack6",
        () => new RpcByteArgumentSerializerV4(NerdbankMessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5(peer));
    public static readonly RpcSerializationFormat NerdbankMessagePackV6C = new("nmsgpack6c",
        () => new RpcByteArgumentSerializerV4(NerdbankMessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5Compact(peer));

    public static void Register()
    {
        var formats = RpcSerializationFormat.All;
        if (!formats.Any(x => string.Equals(x.Key, NerdbankMessagePackV6.Key, StringComparison.Ordinal)))
            formats = formats.Add(NerdbankMessagePackV6);
        if (!formats.Any(x => string.Equals(x.Key, NerdbankMessagePackV6C.Key, StringComparison.Ordinal)))
            formats = formats.Add(NerdbankMessagePackV6C);
        if (!ReferenceEquals(formats, RpcSerializationFormat.All)) {
            RpcSerializationFormat.All = formats;
            // Reset the resolver so it picks up the newly registered formats
            RpcSerializationFormatResolver.DefaultFormats = null!;
        }
    }
}
