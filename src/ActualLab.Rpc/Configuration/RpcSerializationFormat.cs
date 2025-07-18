using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc;

public sealed class RpcSerializationFormat(
    string key,
    Func<RpcArgumentSerializer> argumentSerializerFactory,
    Func<RpcPeer, IByteSerializer<RpcMessage>> messageSerializerFactory)
{
    // Static members

    // "json3"
    public static readonly RpcSerializationFormat SystemJsonV3 = new("json3",
        () => new RpcTextArgumentSerializerV3(SystemJsonSerializer.Default),
        peer => new RpcTextMessageSerializer(peer));

    // "njson3"
    public static readonly RpcSerializationFormat NewtonsoftJsonV3 = new("njson3",
        () => new RpcTextArgumentSerializerV3(NewtonsoftJsonSerializer.Default),
        peer => new RpcTextMessageSerializer(peer));

    // "mempack1"
    public static readonly RpcSerializationFormat MemoryPackV1 = new("mempack1",
        () => new RpcByteArgumentSerializerV1(MemoryPackByteSerializer.Default, forcePolymorphism: true),
        _ => MemoryPackByteSerializer.Default.ToTyped<RpcMessageV1>().Convert(RpcMessageV1.Converter));
    // "mempack2"
    public static readonly RpcSerializationFormat MemoryPackV2 = new("mempack2",
        () => new RpcByteArgumentSerializerV2(MemoryPackByteSerializer.Default, forcePolymorphism: true),
        peer => new RpcByteMessageSerializer(peer));
    public static readonly RpcSerializationFormat MemoryPackV2C = new("mempack2c",
        () => new RpcByteArgumentSerializerV2(MemoryPackByteSerializer.Default, forcePolymorphism: true),
        peer => new RpcByteMessageSerializerCompact(peer));
    // ReSharper disable once InconsistentNaming
    public static readonly RpcSerializationFormat MemoryPackV2NP = new("mempack2-np",
        () => new RpcByteArgumentSerializerV2(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializer(peer));
    // ReSharper disable once InconsistentNaming
    public static readonly RpcSerializationFormat MemoryPackV2CNP = new("mempack2c-np",
        () => new RpcByteArgumentSerializerV2(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializerCompact(peer));
    // "mempack3"
    public static readonly RpcSerializationFormat MemoryPackV3 = new("mempack3",
        () => new RpcByteArgumentSerializerV3(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializer(peer));
    public static readonly RpcSerializationFormat MemoryPackV3C = new("mempack3c",
        () => new RpcByteArgumentSerializerV3(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializerCompact(peer));

    // "msgpack1"
    public static readonly RpcSerializationFormat MessagePackV1 = new("msgpack1",
        () => new RpcByteArgumentSerializerV1(MessagePackByteSerializer.Default, forcePolymorphism: true),
        _ => MessagePackByteSerializer.Default.ToTyped<RpcMessageV1>().Convert(RpcMessageV1.Converter));
    // "msgpack2"
    public static readonly RpcSerializationFormat MessagePackV2 = new("msgpack2",
        () => new RpcByteArgumentSerializerV2(MessagePackByteSerializer.Default, forcePolymorphism: true),
        peer => new RpcByteMessageSerializer(peer));
    public static readonly RpcSerializationFormat MessagePackV2C = new("msgpack2c",
        () => new RpcByteArgumentSerializerV2(MessagePackByteSerializer.Default, forcePolymorphism: true),
        peer => new RpcByteMessageSerializerCompact(peer));
    // ReSharper disable once InconsistentNaming
    public static readonly RpcSerializationFormat MessagePackV2NP = new("msgpack2-np",
        () => new RpcByteArgumentSerializerV2(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializer(peer));
    // ReSharper disable once InconsistentNaming
    public static readonly RpcSerializationFormat MessagePackV2CNP = new("msgpack2c-np",
        () => new RpcByteArgumentSerializerV2(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerCompact(peer));
    // "msgpack3"
    public static readonly RpcSerializationFormat MessagePackV3 = new("msgpack3",
        () => new RpcByteArgumentSerializerV3(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializer(peer));
    public static readonly RpcSerializationFormat MessagePackV3C = new("msgpack3c",
        () => new RpcByteArgumentSerializerV3(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerCompact(peer));

    public static ImmutableList<RpcSerializationFormat> All { get; set; } = ImmutableList.Create(
        SystemJsonV3,
        NewtonsoftJsonV3,
        MemoryPackV1,
        MemoryPackV2, MemoryPackV2C, MemoryPackV2NP, MemoryPackV2CNP,
        MemoryPackV3, MemoryPackV3C,
        MessagePackV1,
        MessagePackV2, MessagePackV2C, MessagePackV2NP, MessagePackV2CNP,
        MessagePackV3, MessagePackV3C);

    // Instance members

    private readonly Lazy<RpcArgumentSerializer> _argumentSerializerLazy = new(argumentSerializerFactory);

    public string Key { get; } = key;
    public RpcArgumentSerializer ArgumentSerializer => _argumentSerializerLazy.Value;
    public Func<RpcPeer, IByteSerializer<RpcMessage>> MessageSerializerFactory => messageSerializerFactory;
}
