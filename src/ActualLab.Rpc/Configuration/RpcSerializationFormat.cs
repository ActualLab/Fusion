using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc;

public sealed class RpcSerializationFormat(
    string key,
    Func<RpcArgumentSerializer> argumentSerializerFactory,
    Func<RpcPeer, RpcMessageSerializer> messageSerializerFactory)
{
    // Static members

    // "json5" - System.Text.Json (legacy)
    public static readonly RpcSerializationFormat SystemJsonV5 = new("json5",
        () => new RpcTextArgumentSerializerV4(SystemJsonSerializer.Default),
        peer => new RpcTextMessageSerializerV3(peer));

    // "njson5" - Newtonsoft.Json (legacy)
    public static readonly RpcSerializationFormat NewtonsoftJsonV5 = new("njson5",
        () => new RpcTextArgumentSerializerV4(NewtonsoftJsonSerializer.Default),
        peer => new RpcTextMessageSerializerV3(peer));

    // "mempack5" - MemoryPack (legacy)
    public static readonly RpcSerializationFormat MemoryPackV5 = new("mempack5",
        () => new RpcByteArgumentSerializerV4(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV4(peer));
    public static readonly RpcSerializationFormat MemoryPackV5C = new("mempack5c",
        () => new RpcByteArgumentSerializerV4(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV4Compact(peer));

    // "msgpack5" - MessagePack (legacy)
    public static readonly RpcSerializationFormat MessagePackV5 = new("msgpack5",
        () => new RpcByteArgumentSerializerV4(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV4(peer));
    public static readonly RpcSerializationFormat MessagePackV5C = new("msgpack5c",
        () => new RpcByteArgumentSerializerV4(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV4Compact(peer));

    // "mempack6" - MemoryPack (current)
    public static readonly RpcSerializationFormat MemoryPackV6 = new("mempack6",
        () => new RpcByteArgumentSerializerV4(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5(peer));
    public static readonly RpcSerializationFormat MemoryPackV6C = new("mempack6c",
        () => new RpcByteArgumentSerializerV4(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5Compact(peer));

    // "msgpack6" - MessagePack (current)
    public static readonly RpcSerializationFormat MessagePackV6 = new("msgpack6",
        () => new RpcByteArgumentSerializerV4(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5(peer));
    public static readonly RpcSerializationFormat MessagePackV6C = new("msgpack6c",
        () => new RpcByteArgumentSerializerV4(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5Compact(peer));

    public static ImmutableList<RpcSerializationFormat> All { get; set; } = ImmutableList.Create(
        SystemJsonV5,
        NewtonsoftJsonV5,
        MemoryPackV5, MemoryPackV5C,
        MessagePackV5, MessagePackV5C,
        MemoryPackV6, MemoryPackV6C,
        MessagePackV6, MessagePackV6C);

    // Instance members

    private readonly Lazy<RpcArgumentSerializer> _argumentSerializerLazy = new(argumentSerializerFactory);

    public string Key { get; } = key;
    public RpcArgumentSerializer ArgumentSerializer => _argumentSerializerLazy.Value;
    public Func<RpcPeer, RpcMessageSerializer> MessageSerializerFactory => messageSerializerFactory;
}
