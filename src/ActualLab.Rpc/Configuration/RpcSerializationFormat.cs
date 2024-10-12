using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc;

public sealed class RpcSerializationFormat(
    Symbol key,
    Func<RpcArgumentSerializer> argumentSerializerFactory,
    Func<RpcPeer, IByteSerializer<RpcMessage>> messageSerializerFactory)
{
    // Static members

   private static readonly RpcSerializationFormat SystemJson = new("json",
            () => new RpcTextArgumentSerializer(SystemJsonSerializer.Default),
            peer => new RpcTextMessageSerializer(peer));
   private static readonly RpcSerializationFormat NewtonsoftJson = new("njson",
            () => new RpcTextArgumentSerializer(NewtonsoftJsonSerializer.Default),
            peer => new RpcTextMessageSerializer(peer));

   private static readonly RpcSerializationFormat MemoryPackV1 = new("mempack1",
            () => new RpcByteArgumentSerializerV1(MemoryPackByteSerializer.Default),
            _ => MemoryPackByteSerializer.Default.ToTyped<RpcMessageV1>().Convert(RpcMessageV1.Converter));
   private static readonly RpcSerializationFormat MemoryPackV2 = new("mempack2",
            () => new RpcByteArgumentSerializer(MemoryPackByteSerializer.Default),
            peer => new RpcByteMessageSerializer(peer));
   private static readonly RpcSerializationFormat MemoryPackV2C = new("mempack2c",
            () => new RpcByteArgumentSerializer(MemoryPackByteSerializer.Default),
            peer => new RpcByteMessageSerializerCompact(peer));
   private static readonly RpcSerializationFormat MemoryPackV2A = new("mempack2a",
            () => new RpcByteArgumentSerializer(MemoryPackByteSerializer.Default),
            _ => MemoryPackByteSerializer.Default.ToTyped<RpcMessage>());

   private static readonly RpcSerializationFormat MessagePackV1 = new("msgpack1",
            () => new RpcByteArgumentSerializerV1(MessagePackByteSerializer.Default),
            _ => MessagePackByteSerializer.Default.ToTyped<RpcMessageV1>().Convert(RpcMessageV1.Converter));
   private static readonly RpcSerializationFormat MessagePackV2 = new("msgpack2",
            () => new RpcByteArgumentSerializer(MessagePackByteSerializer.Default),
            peer => new RpcByteMessageSerializer(peer));
   private static readonly RpcSerializationFormat MessagePackV2C = new("msgpack2c",
            () => new RpcByteArgumentSerializer(MessagePackByteSerializer.Default),
            peer => new RpcByteMessageSerializerCompact(peer));
   private static readonly RpcSerializationFormat MessagePackV2A = new("msgpack2a",
            () => new RpcByteArgumentSerializer(MessagePackByteSerializer.Default),
            _ => MessagePackByteSerializer.Default.ToTyped<RpcMessage>());

   // ReSharper disable once UseCollectionExpression
   private static readonly ImmutableArray<RpcSerializationFormat> All = ImmutableArray.Create(
        MemoryPackV1, MemoryPackV2, MemoryPackV2C, MemoryPackV2A,
        MessagePackV1, MessagePackV2, MessagePackV2C, MessagePackV2A,
        SystemJson, NewtonsoftJson);

    // Instance members

    private readonly Lazy<RpcArgumentSerializer> _argumentSerializerLazy = new(argumentSerializerFactory);

    public Symbol Key { get; } = key;
    public RpcArgumentSerializer ArgumentSerializer => _argumentSerializerLazy.Value;
    public Func<RpcPeer, IByteSerializer<RpcMessage>> MessageSerializerFactory => messageSerializerFactory;
}
