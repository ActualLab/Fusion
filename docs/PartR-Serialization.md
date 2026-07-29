# RPC Serialization Formats

ActualLab.Rpc supports multiple serialization formats with automatic version negotiation between clients and servers.
This enables gradual upgrades and interoperability between different Fusion versions.

## Overview

RPC serialization has two layers:

1. **Argument serialization**: How method arguments are encoded
2. **Message serialization**: How RPC messages (containing arguments) are framed

Each combination of these is packaged as an `RpcSerializationFormat`.


## Available Formats

### Text Formats (JSON)

| Format | Key | Description |
|--------|-----|-------------|
| `SystemJsonV5` | `json5` | System.Text.Json with V4 arguments, V3 messages |
| `SystemJsonV5NP` | `json5np` | System.Text.Json, no-polymorphism variant (plain JSON, no `TypeRef` wrapper) |
| `NewtonsoftJsonV5` | `njson5` | Newtonsoft.Json with V4 arguments, V3 messages. **Not client-selectable by default** &mdash; see [Client-Selectable Formats](#client-selectable-formats) |
| `NewtonsoftJsonV5NP` | `njson5np` | Newtonsoft.Json, no-polymorphism variant (plain JSON, no `TypeRef` wrapper). **Not client-selectable by default** |

The "NP" (no-polymorphism) variants skip the type-decorating `TypeRef` wrapper entirely, producing plain JSON without type metadata. Use them when all argument and result types are concrete (non-abstract) and no polymorphic dispatch is needed.

### Binary Formats (MemoryPack)

| Format          | Key | Description           |
|-----------------|-----|-----------------------|
| `MemoryPackV5`  | `mempack5` | V4 args, V4 messages  |
| `MemoryPackV5C` | `mempack5c` | Compact variant of V5 |
| `MemoryPackV6`  | `mempack6` | V4 args, V5 messages  |
| `MemoryPackV6C` | `mempack6c` | Compact variant of V6 |

### Binary Formats (MessagePack)

| Format           | Key | Description           |
|------------------|-----|-----------------------|
| `MessagePackV5`  | `msgpack5` | V4 args, V4 messages  |
| `MessagePackV5C` | `msgpack5c` | Compact variant of V5 |
| `MessagePackV6`  | `msgpack6` | V4 args, V5 messages  |
| `MessagePackV6C` | `msgpack6c` | Compact variant of V6 |

### Binary Formats (Nerdbank.MessagePack)

These formats require the `ActualLab.Serialization.NerdbankMessagePack` package.
They are not registered by default &mdash; call `RpcNerdbankSerializationFormat.Register()` at startup to enable them.

| Format                  | Key          | Description                       |
|-------------------------|--------------|-----------------------------------|
| `NerdbankMessagePackV6` | `nmsgpack6`  | Nerdbank.MessagePack, V4 args, V5 messages |
| `NerdbankMessagePackV6C`| `nmsgpack6c` | Compact variant of V6             |

## Format Selection

### Default Format

The default is `MemoryPackV6` (`mempack6`) &mdash; it's the key `RpcSerializationFormatResolver.Default` is created with. Override it process-wide by assigning a new resolver:

```cs
RpcSerializationFormatResolver.Default = new RpcSerializationFormatResolver(
    RpcSerializationFormat.MessagePackV6C.Key);
```

### Client-Server Negotiation

When a client connects, it requests its preferred serialization format via a URL parameter (e.g., `<endpoint>?f=msgpack6&clientId=...`). The server accepts the connection if it supports that format **and allows clients to select it**. Once connected, both parties simultaneously exchange `RpcHandshake` messages:

<img src="/img/diagrams/PartR-Serialization-1.svg" alt="Client-Server Negotiation" style="width: 100%; max-width: 800px;" />

### Accessing All Formats

<!-- snippet: PartRSerialization_AccessingFormats -->
```cs
// All registered formats
ImmutableList<RpcSerializationFormat> all = RpcSerializationFormat.All;

// Find by key
var format = RpcSerializationFormat.All.First(f => f.Key == "mempack6c");
```
<!-- endSnippet -->

### Client-Selectable Formats

A registered format isn't automatically a format a *client* may pin via `?f=…`.
`RpcSerializationFormatResolver.ClientDeniedFormatKeys` is the allow-list's complement:
every RPC server endpoint (WebSocket, HTTP/2, OWIN) rejects a connection whose requested
key is in it. An empty `f` still means "server picks", so it's always accepted.

**Since v14.2, `njson5` and `njson5np` are denied to clients by default.** Newtonsoft-backed
formats deserialize with `TypeNameHandling.Auto` and no `SerializationBinder`, so they honor
nested `$type` markers &mdash; a gadget surface the other formats lack. Server-to-server and
in-process use of these formats is unaffected; only client-pinned selection is blocked.

To restore the previous behavior:

```cs
RpcSerializationFormatResolver.DefaultClientDeniedFormatKeys = ImmutableHashSet<string>.Empty;
```

Or to deny more, e.g. the legacy V5 formats:

```cs
RpcSerializationFormatResolver.DefaultClientDeniedFormatKeys
    = RpcSerializationFormatResolver.DefaultClientDeniedFormatKeys
        .Add(RpcSerializationFormat.MemoryPackV5.Key)
        .Add(RpcSerializationFormat.MessagePackV5.Key);
```

`DefaultClientDeniedFormatKeys` seeds `ClientDeniedFormatKeys` on every resolver created after
it's assigned, so set it at startup, before the first `RpcSerializationFormatResolver` is built.


## Format Structure

Each `RpcSerializationFormat` consists of:

<!-- snippet: PartRSerialization_FormatStructure -->
```cs
public sealed class RpcSerializationFormatExample(
    string key,
    Func<RpcArgumentSerializer> argumentSerializerFactory,
    Func<RpcPeer, RpcMessageSerializer> messageSerializerFactory)
{
    public string Key { get; } = key;
    public RpcArgumentSerializer ArgumentSerializer { get; } = argumentSerializerFactory();
    public Func<RpcPeer, RpcMessageSerializer> MessageSerializerFactory { get; } = messageSerializerFactory;
}
```
<!-- endSnippet -->

| Property | Description |
|----------|-------------|
| `Key` | Unique string identifier for negotiation |
| `ArgumentSerializer` | Serializes method arguments |
| `MessageSerializerFactory` | Creates message serializers per peer |


## Version Differences

### Argument Serializer Versions

| Version | Description |
|---------|-------------|
| V4      | Latest, best performance |

### Message Serializer Versions

| Version | Variants | Description                      |
|---------|----------|----------------------------------|
| V4      | Normal, Compact | Was optimal up to Fusion v11.5.X |
| V5      | Normal, Compact | Saves 1 byte per message over V4 |

### Compact vs Normal

Compact variants (`*C` suffix) use smaller message framing at a slight CPU cost. Choose compact for:
- Lower bandwidth scenarios
- When message overhead is significant relative to payload


## Configuring Formats

### Enabling Nerdbank.MessagePack Formats

Add the `ActualLab.Serialization.NerdbankMessagePack` package and call `Register()` at startup:

```cs
// Register nmsgpack6 / nmsgpack6c formats
RpcNerdbankSerializationFormat.Register();
```

### Registering Additional Formats

<!-- snippet: PartRSerialization_RegisterFormat -->
```cs
RpcSerializationFormat.All = RpcSerializationFormat.All.Add(
    new RpcSerializationFormat(
        "custom",
        () => new MyArgumentSerializer(),
        peer => new MyMessageSerializer(peer)));
```
<!-- endSnippet -->

### Removing Formats

To disable older formats for security:

<!-- snippet: PartRSerialization_RemoveFormats -->
```cs
// To disable older formats for security:
RpcSerializationFormat.All = RpcSerializationFormat.All
    .RemoveAll(f => f.Key.StartsWith("mempack5") || f.Key.StartsWith("msgpack5"));
```
<!-- endSnippet -->


## Format Selection Factors

When choosing formats, consider:

| Factor | Recommendation |
|--------|----------------|
| Performance | Binary formats (MemoryPack > MessagePack > JSON) |
| Debugging | JSON formats (human-readable) |
| Compatibility | MessagePack for .NET Standard clients |
| Bandwidth | Compact variants (`*C`) |
| Security | Latest versions; keep the Newtonsoft formats out of clients' reach (the default) |


## Serialization in RPC Pipeline

<img src="/img/diagrams/PartR-Serialization-2.svg" alt="Serialization in RPC Pipeline" style="width: 100%; max-width: 800px;" />

1. Client serializes method arguments using `ArgumentSerializer`
2. Arguments are wrapped in an `RpcMessage` and serialized by `MessageSerializer`
3. Binary data is sent over WebSocket
4. Server deserializes in reverse order


## Size Limits

Every RPC size ceiling is explicit and enforced **on both the send and the receive path**.
They were tightened substantially in v14.2 &mdash; the pre-14.2 values are shown for
comparison, since a peer that relied on the old headroom will now be rejected:

| Limit | Declared on | v14.2 | Before |
|-------|-------------|-------|--------|
| Max frame (= one batch of messages) | `RpcFrameBasedTransport.DefaultMaxFrameSize` | 16,711,680 (16 MiB &minus; 64 KiB) | 33,489,152 |
| Max pre-handshake frame | `RpcFrameBasedTransport.DefaultMaxPreHandshakeFrameSize` | 16,384 | *same as the frame limit* |
| Max argument data per message | `RpcByteMessageSerializer.Defaults.MaxArgumentDataSize`, `RpcTextMessageSerializer.Defaults.MaxArgumentDataSize` | 16,252,928 (15.5 MiB) | 16 MiB |
| Max method reference (UTF-8) | `RpcMethodRef.MaxUtf8NameLength`, `RpcByteMessageSerializer.MaxMethodRefSize` | 1,024 | 65,536 |
| Max single header value | `RpcByteMessageSerializer.MaxHeaderSize` | 1,024 | 65,536 |
| Max headers per message (text formats) | `RpcTextMessageSerializerV3.MaxHeaderCount` | 31 | 31 |
| Max text envelope | `RpcTextMessageSerializerV3.MaxEnvelopeSize` | 244,297 | 12,261,961 |
| Max API version set | `RpcHandshake.MaxApiVersionSetCount` / `MaxApiVersionSetLength` | 16 scopes / 512 chars | *unbounded* |

The frame limit sits 64 KiB *below* the 16 MiB `ArrayPool` bucket on purpose: `ArrayPoolBuffer`
rounds every capacity request up to the next power of two, and `RpcStreamTransport` buffers a
4-byte length prefix plus read-ahead alongside the frame &mdash; without the reserve, a
maximum-size frame would push its receive buffer into the next (32 MiB) bucket.
`MaxArgumentDataSize` is 15.5 MiB rather than a round 16 MiB for the same reason: the payload,
the worst-case envelope of the most expensive registered format and the frame delimiter must
all fit one frame.

An over-limit header, method reference or payload is rejected while reading, and the message is
dropped **without an error reply** &mdash; the remote peer sees the call as never answered.
Raise `MaxArgumentDataSize` (on both serializer base classes) if you genuinely move payloads
this large, but keep the frame ceiling in mind: `RpcWebSocketTransportSizeTest` pins that a
maximum-size message still fits a maximum-size frame in every registered format.

W3C trace context is bounded by its own spec limits too: an over-length `tracestate` is dropped
while its `traceparent` is still adopted.


## Polymorphic Serialization

By default, ActualLab.Rpc treats abstract types and `object` as polymorphic.
When a method argument or result is polymorphic, the serializer wraps it with a `TypeRef`
so the actual runtime type can be restored on the other side.

This is determined by `RpcArgumentSerializer.IsPolymorphic(Type)`:

```cs
// These are considered polymorphic by default:
IsPolymorphic(typeof(ITuple))  // true - it's an interface (abstract)
IsPolymorphic(typeof(object))  // true

// Concrete types are not:
IsPolymorphic(typeof(string))  // false
IsPolymorphic(typeof(int))     // false

// Since v14.2 arrays are seen through — the element type decides:
IsPolymorphic(typeof(Shape[]))  // true if Shape is polymorphic
IsPolymorphic(typeof(int[]))    // false
```

Before v14.2 an array was never polymorphic (an array type isn't abstract), which forced the
stream-batch path to widen its declared argument type to `object` just to reach the polymorphic
serializer &mdash; and the declared type is also the bound that a wire-supplied type name is
checked against. Recursing into the element type keeps the declared type exact, so the accepted
set equals the producible set.

### Opting Out with `[RpcSerializable]`

When the underlying serializer already handles polymorphism
(e.g., via `[JsonDerivedType]`, `[MemoryPackUnion]`, or `[Union]`),
the RPC layer's `TypeRef` wrapping is redundant overhead.
Apply `[RpcSerializable]` to the base type to tell RPC that
the type can be serialized directly:

<!-- snippet: PartRSerialization_RpcSerializableAttribute -->
```cs
// The underlying serializers handle polymorphism via union attributes,
// so we mark this type as RPC-serializable to opt out of TypeRef wrapping.
[RpcSerializable]
[MemoryPackable]
[MemoryPackUnion(0, typeof(ShapeCircle))]
[MemoryPackUnion(1, typeof(ShapeRect))]
[MessagePackObject]
[Union(0, typeof(ShapeCircle))]
[Union(1, typeof(ShapeRect))]
[JsonDerivedType(typeof(ShapeCircle), "circle")]
[JsonDerivedType(typeof(ShapeRect), "rect")]
public abstract partial class Shape
{
    [DataMember, MemoryPackOrder(0), Key(0)]
    public string? Name { get; set; }
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public partial class ShapeCircle : Shape
{
    [DataMember, MemoryPackOrder(1), Key(1)]
    public double Radius { get; set; }
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public partial class ShapeRect : Shape
{
    [DataMember, MemoryPackOrder(1), Key(1)]
    public double Width { get; set; }

    [DataMember, MemoryPackOrder(2), Key(2)]
    public double Height { get; set; }
}
```
<!-- endSnippet -->

With this attribute, `RpcArgumentSerializer.IsPolymorphic(typeof(Shape))` returns `false`,
so methods like `Task<Shape> GetShape(...)` use regular serialization.
The discriminated union support in each serializer takes care of preserving
the actual runtime type.

The attribute uses `Inherited = true`, so derived types also inherit the opt-out.

### When to Use

Use `[RpcSerializable]` when:

- Your abstract base type or interface has serializer-level union support
  (`[JsonDerivedType]`, `[MemoryPackUnion]`, `[Union]`)
- You want to avoid the overhead of RPC's `TypeRef` wrapping
- All concrete subtypes are declared in the union configuration


## Related Topics

- [Core Serialization](./PartS.md) - General serialization infrastructure
- [RPC Key Concepts](./PartR-CC.md) - RPC architecture overview
- [Configuration Options](./PartR-CO.md) - RPC configuration
