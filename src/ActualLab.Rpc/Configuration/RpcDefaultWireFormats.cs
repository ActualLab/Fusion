using ActualLab.Interception.Serialization;
using ActualLab.Rpc.Serialization;

// ReSharper disable InconsistentNaming

namespace ActualLab.Rpc;

/// <summary>
/// The <see cref="RpcWireFormat"/>s of ActualLab.Rpc. MemoryPack, Newtonsoft.Json and
/// Nerdbank.MessagePack formats are provided by their own packages.
/// </summary>
public static class RpcDefaultWireFormats
{
    // "json5" - System.Text.Json
    public static readonly RpcWireFormat SystemJsonV5 = new("json5",
        () => new TextArgumentListSerializer(SystemJsonSerializer.Default),
        peer => new RpcTextMessageSerializerV3(peer),
        DataFormat.Text);
    // "json5np" - System.Text.Json, no polymorphism
    public static readonly RpcWireFormat SystemJsonV5NP = new("json5np",
        () => new TextArgumentListSerializerNP(SystemJsonSerializer.Default),
        peer => new RpcTextMessageSerializerV3(peer),
        DataFormat.Text);

    // "msgpack5" - MessagePack (legacy)
    public static readonly RpcWireFormat MessagePackV5 = new("msgpack5",
        () => new ByteArgumentListSerializer(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV4(peer),
        DataFormat.Bytes);
    public static readonly RpcWireFormat MessagePackV5C = new("msgpack5c",
        () => new ByteArgumentListSerializer(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV4Compact(peer),
        DataFormat.Bytes);

    // "msgpack6" - MessagePack (current)
    public static readonly RpcWireFormat MessagePackV6 = new("msgpack6",
        () => new ByteArgumentListSerializer(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5(peer),
        DataFormat.Bytes);
    public static readonly RpcWireFormat MessagePackV6C = new("msgpack6c",
        () => new ByteArgumentListSerializer(MessagePackByteSerializer.Default),
        peer => new RpcByteMessageSerializerV5Compact(peer),
        DataFormat.Bytes);

    // "msgpack6k" / "msgpack6ck" - keyed MessagePack: the [MessagePackObject] / [DataContract] types of your app
    // are string-keyed maps, so e.g. a TypeScript client reads them by member name rather than [Key(N)].
    // Not in the default set (add them with f.AddKeyedMessagePack()); they don't work under NativeAOT.
    public static readonly RpcWireFormat MessagePackV6K = new("msgpack6k",
        () => new ByteArgumentListSerializer(MessagePackByteSerializer.DefaultKeyed),
        peer => new RpcByteMessageSerializerV5(peer),
        DataFormat.Bytes);
    public static readonly RpcWireFormat MessagePackV6CK = new("msgpack6ck",
        () => new ByteArgumentListSerializer(MessagePackByteSerializer.DefaultKeyed),
        peer => new RpcByteMessageSerializerV5Compact(peer),
        DataFormat.Bytes);

    // The default format of every container unless rpc.ConfigureWireFormats(...) changes it
    public static RpcWireFormat DefaultFormat => MessagePackV6;

    // The formats every RpcWireFormatConfiguration starts with: rpc.ConfigureWireFormats(...) adds more,
    // e.g. compressed variants (AddCompressedVariants) or the formats of the serialization packages
    public static readonly ImmutableList<RpcWireFormat> All = ImmutableList.Create(
        SystemJsonV5, SystemJsonV5NP,
        MessagePackV5, MessagePackV5C,
        MessagePackV6, MessagePackV6C);

    // The format keys only backends (servers, in-process peers) may use: RPC servers reject a client
    // connection that selects one. It seeds every RpcWireFormatConfiguration and resolver.
    // Newtonsoft-backed formats deserialize with TypeNameHandling.Auto and no SerializationBinder,
    // so they additionally honor nested "$type" markers - a gadget surface the other formats lack.
    // Defence in depth rather than a complete fix: polymorphic RPC arguments carry an out-of-band
    // type name that TypeRef.Resolve turns into a Type under every format.
    // To allow them in one container:
    // rpc.ConfigureWireFormats(f => f.BackendOnlyFormatKeys = f.BackendOnlyFormatKeys.Clear())
    public static ImmutableHashSet<string> BackendOnlyFormatKeys { get; }
        = ImmutableHashSet.Create(StringComparer.Ordinal,
            "njson5", "njson5np"); // ActualLab.Serialization.NewtonsoftJson formats
}
