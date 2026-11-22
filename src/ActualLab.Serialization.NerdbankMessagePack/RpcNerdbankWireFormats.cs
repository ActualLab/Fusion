using ActualLab.Interception.Serialization;
using ActualLab.Rpc;
using ActualLab.Rpc.Compression;
using ActualLab.Rpc.Serialization;

// ReSharper disable InconsistentNaming

namespace ActualLab.Serialization;

public static class RpcNerdbankWireFormats
{
    public static readonly RpcWireFormat NerdbankMessagePackV6 = new("nmsgpack6",
        () => new ByteArgumentListSerializer(NerdbankMessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5(peer),
        DataFormat.Bytes);
    public static readonly RpcWireFormat NerdbankMessagePackV6C = new("nmsgpack6c",
        () => new ByteArgumentListSerializer(NerdbankMessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5Compact(peer),
        DataFormat.Bytes);


    public static readonly ImmutableList<RpcWireFormat> All = ImmutableList.Create(
        NerdbankMessagePackV6, NerdbankMessagePackV6C);
}
