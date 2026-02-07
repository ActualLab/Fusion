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
        RpcSerializationFormat.All = RpcSerializationFormat.All
            .Add(NerdbankMessagePackV6)
            .Add(NerdbankMessagePackV6C);
    }
}
