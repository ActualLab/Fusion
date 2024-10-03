using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc;

public sealed class RpcSerializationFormat(
    Symbol key,
    Func<RpcArgumentSerializer> argumentSerializerFactory,
    Func<RpcPeer, IByteSerializer<RpcMessage>> messageSerializerFactory)
{
    // Static members

    private static readonly LazySlim<RpcSerializationFormat> SystemJsonLazy = new(
        () => new RpcSerializationFormat("json", // Doesn't work yet
            () => new RpcTextArgumentSerializer(SystemJsonSerializer.Default),
            _ => SystemJsonSerializer.Default.ToTyped<RpcMessageV1>().Convert(RpcMessageV1.Converter)));
    private static readonly LazySlim<RpcSerializationFormat> NewtonsoftJsonLazy = new(
        () => new RpcSerializationFormat("njson", // Doesn't work yet
            () => new RpcTextArgumentSerializer(NewtonsoftJsonSerializer.Default),
            _ => NewtonsoftJsonSerializer.Default.ToTyped<RpcMessageV1>().Convert(RpcMessageV1.Converter)));

    private static readonly LazySlim<RpcSerializationFormat> MemoryPackV1Lazy = new(
        () => new RpcSerializationFormat("mempack1",
            () => new RpcByteArgumentSerializerV1(MemoryPackByteSerializer.Default),
            _ => MemoryPackByteSerializer.Default.ToTyped<RpcMessageV1>().Convert(RpcMessageV1.Converter)));
    private static readonly LazySlim<RpcSerializationFormat> MemoryPackV2Lazy = new(
        () => new RpcSerializationFormat("mempack2",
            () => new RpcByteArgumentSerializer(MemoryPackByteSerializer.Default),
            peer => new RpcByteMessageSerializer(peer)));
    private static readonly LazySlim<RpcSerializationFormat> MemoryPackV2CLazy = new(
        () => new RpcSerializationFormat("mempack2c",
            () => new RpcByteArgumentSerializer(MemoryPackByteSerializer.Default),
            peer => new RpcByteMessageSerializerCompact(peer)));
    private static readonly LazySlim<RpcSerializationFormat> MemoryPackV2ALazy = new(
        () => new RpcSerializationFormat("mempack2a",
            () => new RpcByteArgumentSerializer(MemoryPackByteSerializer.Default),
            _ => MemoryPackByteSerializer.Default.ToTyped<RpcMessage>()));

    private static readonly LazySlim<RpcSerializationFormat> MessagePackV1Lazy = new(
        () => new RpcSerializationFormat("msgpack1",
            () => new RpcByteArgumentSerializerV1(MessagePackByteSerializer.Default),
            _ => MessagePackByteSerializer.Default.ToTyped<RpcMessageV1>().Convert(RpcMessageV1.Converter)));
    private static readonly LazySlim<RpcSerializationFormat> MessagePackV2Lazy = new(
        () => new RpcSerializationFormat("msgpack2",
            () => new RpcByteArgumentSerializer(MessagePackByteSerializer.Default),
            peer => new RpcByteMessageSerializer(peer)));
    private static readonly LazySlim<RpcSerializationFormat> MessagePackV2CLazy = new(
        () => new RpcSerializationFormat("msgpack2c",
            () => new RpcByteArgumentSerializer(MessagePackByteSerializer.Default),
            peer => new RpcByteMessageSerializerCompact(peer)));
    private static readonly LazySlim<RpcSerializationFormat> MessagePackV2ALazy = new(
        () => new RpcSerializationFormat("msgpack2a",
            () => new RpcByteArgumentSerializer(MessagePackByteSerializer.Default),
            _ => MessagePackByteSerializer.Default.ToTyped<RpcMessage>()));

    private static readonly LazySlim<ImmutableArray<RpcSerializationFormat>> AllLazy =
        new(ImmutableArray.Create(
            // SystemJson, NewtonsoftJson,
            MemoryPackV1, MemoryPackV2, MemoryPackV2C, MemoryPackV2A,
            MessagePackV1, MessagePackV2, MessagePackV2C, MessagePackV2A));

    // public static RpcSerializationFormat SystemJson => SystemJsonLazy.Value;
    // public static RpcSerializationFormat NewtonsoftJson => NewtonsoftJsonLazy.Value;
    public static RpcSerializationFormat MemoryPackV1 => MemoryPackV1Lazy.Value;
    public static RpcSerializationFormat MemoryPackV2 => MemoryPackV2Lazy.Value;
    public static RpcSerializationFormat MemoryPackV2C => MemoryPackV2CLazy.Value;
    public static RpcSerializationFormat MemoryPackV2A => MemoryPackV2ALazy.Value;
    public static RpcSerializationFormat MessagePackV1 => MessagePackV1Lazy.Value;
    public static RpcSerializationFormat MessagePackV2 => MessagePackV2Lazy.Value;
    public static RpcSerializationFormat MessagePackV2C => MessagePackV2CLazy.Value;
    public static RpcSerializationFormat MessagePackV2A => MessagePackV2ALazy.Value;
    public static ImmutableArray<RpcSerializationFormat> All => AllLazy.Value;

    // Instance members

    private readonly LazySlim<RpcArgumentSerializer> _argumentSerializerLazy = new(argumentSerializerFactory);

    public Symbol Key { get; } = key;
    public RpcArgumentSerializer ArgumentSerializer => _argumentSerializerLazy.Value;
    public Func<RpcPeer, IByteSerializer<RpcMessage>> MessageSerializerFactory => messageSerializerFactory;
}
