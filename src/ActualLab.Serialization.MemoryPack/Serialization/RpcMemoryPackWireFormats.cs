using ActualLab.Interception.Serialization;
using ActualLab.Rpc;
using ActualLab.Rpc.Compression;
using ActualLab.Rpc.Serialization;

// ReSharper disable InconsistentNaming

namespace ActualLab.Serialization;

/// <summary>
/// MemoryPack-based <see cref="RpcWireFormat"/>s.
/// Use <see cref="RpcWireFormatConfigurationExt.AddMemoryPack"/> to add them to RPC.
/// </summary>
public static class RpcMemoryPackWireFormats
{
    // "mempack5" - MemoryPack (legacy)
    public static readonly RpcWireFormat MemoryPackV5 = new("mempack5",
        () => new ByteArgumentListSerializer(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV4(peer),
        DataFormat.Bytes);
    public static readonly RpcWireFormat MemoryPackV5C = new("mempack5c",
        () => new ByteArgumentListSerializer(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV4Compact(peer),
        DataFormat.Bytes);

    // "mempack6" - MemoryPack (current)
    public static readonly RpcWireFormat MemoryPackV6 = new("mempack6",
        () => new ByteArgumentListSerializer(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5(peer),
        DataFormat.Bytes);
    public static readonly RpcWireFormat MemoryPackV6C = new("mempack6c",
        () => new ByteArgumentListSerializer(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5Compact(peer),
        DataFormat.Bytes);

    public static readonly ImmutableList<RpcWireFormat> All = ImmutableList.Create(
        MemoryPackV5, MemoryPackV5C,
        MemoryPackV6, MemoryPackV6C);
}
