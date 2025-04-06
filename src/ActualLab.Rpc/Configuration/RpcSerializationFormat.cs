using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc;

public sealed class RpcSerializationFormat(
    string key,
    Func<RpcArgumentSerializer> argumentSerializerFactory,
    Func<RpcPeer, IByteSerializer<RpcMessage>> messageSerializerFactory)
{
    // Static members

    public static readonly RpcSerializationFormat SystemJson = new("json",
        () => new RpcTextArgumentSerializer(SystemJsonSerializer.Default),
        peer => new RpcTextMessageSerializer(peer));
    public static readonly RpcSerializationFormat SystemJsonNP = new("json-np",
        () => new RpcTextArgumentSerializer(SystemJsonSerializer.Default, allowPolymorphism: false),
        peer => new RpcTextMessageSerializer(peer));

    public static readonly RpcSerializationFormat NewtonsoftJson = new("njson",
        () => new RpcTextArgumentSerializer(NewtonsoftJsonSerializer.Default),
        peer => new RpcTextMessageSerializer(peer));
    public static readonly RpcSerializationFormat NewtonsoftJsonNP = new("njson-np",
        () => new RpcTextArgumentSerializer(NewtonsoftJsonSerializer.Default, allowPolymorphism: false),
        peer => new RpcTextMessageSerializer(peer));

   public static readonly RpcSerializationFormat MemoryPackV1 = new("mempack1",
        () => new RpcByteArgumentSerializerV1(MemoryPackByteSerializer.Default),
        _ => MemoryPackByteSerializer.Default.ToTyped<RpcMessageV1>().Convert(RpcMessageV1.Converter));
   public static readonly RpcSerializationFormat MemoryPackV2 = new("mempack2",
        () => new RpcByteArgumentSerializer(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializer(peer));
   public static readonly RpcSerializationFormat MemoryPackV2C = new("mempack2c",
        () => new RpcByteArgumentSerializer(MemoryPackByteSerializer.Default),
        peer => new RpcByteMessageSerializerCompact(peer));
   public static readonly RpcSerializationFormat MemoryPackV2NP = new("mempack2-np",
       () => new RpcByteArgumentSerializer(MemoryPackByteSerializer.Default, allowPolymorphism: false),
       peer => new RpcByteMessageSerializer(peer));
   public static readonly RpcSerializationFormat MemoryPackV2CNP = new("mempack2c-np",
       () => new RpcByteArgumentSerializer(MemoryPackByteSerializer.Default, allowPolymorphism: false),
       peer => new RpcByteMessageSerializerCompact(peer));

   public static readonly RpcSerializationFormat MessagePackV1 = new("msgpack1",
        () => new RpcByteArgumentSerializerV1(MessagePackByteSerializer.Default),
        _ => MessagePackByteSerializer.Default.ToTyped<RpcMessageV1>().Convert(RpcMessageV1.Converter));
   public static readonly RpcSerializationFormat MessagePackV2 = new("msgpack2",
        () => new RpcByteArgumentSerializer(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializer(peer));
   public static readonly RpcSerializationFormat MessagePackV2C = new("msgpack2c",
        () => new RpcByteArgumentSerializer(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerCompact(peer));
   public static readonly RpcSerializationFormat MessagePackV2NP = new("msgpack2-np",
       () => new RpcByteArgumentSerializer(MessagePackByteSerializer.Default, allowPolymorphism: false),
       peer => new RpcByteMessageSerializer(peer));
   public static readonly RpcSerializationFormat MessagePackV2CNP = new("msgpack2c-np",
       () => new RpcByteArgumentSerializer(MessagePackByteSerializer.Default, allowPolymorphism: false),
       peer => new RpcByteMessageSerializerCompact(peer));

   // ReSharper disable once UseCollectionExpression
   public static ImmutableList<RpcSerializationFormat> All { get; set; } = ImmutableList.Create(
        MemoryPackV1, MemoryPackV2, MemoryPackV2C, MemoryPackV2NP, MemoryPackV2CNP,
        MessagePackV1, MessagePackV2, MessagePackV2C, MessagePackV2NP, MessagePackV2CNP,
        SystemJson, SystemJsonNP,
        NewtonsoftJson, NewtonsoftJsonNP);

    // Instance members

    private readonly Lazy<RpcArgumentSerializer> _argumentSerializerLazy = new(argumentSerializerFactory);

    public string Key { get; } = key;
    public RpcArgumentSerializer ArgumentSerializer => _argumentSerializerLazy.Value;
    public Func<RpcPeer, IByteSerializer<RpcMessage>> MessageSerializerFactory => messageSerializerFactory;
}
