# RPC Wire Formats

ActualLab.Rpc supports multiple wire formats with automatic version negotiation between clients and servers.
This enables gradual upgrades and interoperability between different Fusion versions.

## Overview

RPC serialization has two layers:

1. **Argument serialization**: How method arguments are encoded
2. **Message serialization**: How RPC messages (containing arguments) are framed

Each combination of these is packaged as an `RpcWireFormat`.


## Available Formats

`ActualLab.Rpc` ships the System.Text.Json and MessagePack formats. The MemoryPack,
Newtonsoft.Json and Nerdbank.MessagePack formats live in their own packages, and every
format outside the default set is opt-in through
[`ConfigureWireFormats`](#configuring-formats).

The **default set** &mdash; what a container serves when you don't configure it &mdash; is
`json5`, `json5np`, `msgpack5`, `msgpack5c`, `msgpack6` and `msgpack6c`.

### Text Formats (System.Text.Json)

| Format | Key | Description |
|--------|-----|-------------|
| `SystemJsonV5` | `json5` | System.Text.Json with V4 arguments, V3 messages |
| `SystemJsonV5NP` | `json5np` | System.Text.Json, no-polymorphism variant (plain JSON, no `TypeRef` wrapper) |

The "NP" (no-polymorphism) variants skip the type-decorating `TypeRef` wrapper entirely, producing plain JSON without type metadata. Use them when all argument and result types are concrete (non-abstract) and no polymorphic dispatch is needed.

### Binary Formats (MessagePack)

| Format           | Key | Description           |
|------------------|-----|-----------------------|
| `MessagePackV5`  | `msgpack5` | V4 args, V4 messages  |
| `MessagePackV5C` | `msgpack5c` | Compact variant of V5 |
| `MessagePackV6`  | `msgpack6` | V4 args, V5 messages; **the default format** |
| `MessagePackV6C` | `msgpack6c` | Compact variant of V6 |

These are static members of `RpcDefaultWireFormats`, and its `All` lists them:
every container starts with this set. Their LZ4 variants (`msgpack6-lz4`, `msgpack6c-lz4`,
`msgpack6-lz4f`, `msgpack6c-lz4f`, ...) aren't static members; add them with `f.AddLZ4Variants()`.

### Binary Formats (MemoryPack)

These formats require the `ActualLab.Serialization.MemoryPack` package (.NET Core 3.1+ /
.NET Standard 2.1+, no .NET Framework). They are static members of
`RpcMemoryPackWireFormats`; add them with `f.AddMemoryPack()`, and their LZ4 variants
with `f.AddLZ4Variants()`.

| Format          | Key | Description           |
|-----------------|-----|-----------------------|
| `MemoryPackV5`  | `mempack5` | V4 args, V4 messages  |
| `MemoryPackV5C` | `mempack5c` | Compact variant of V5 |
| `MemoryPackV6`  | `mempack6` | V4 args, V5 messages  |
| `MemoryPackV6C` | `mempack6c` | Compact variant of V6 |

The package produces the same bytes earlier Fusion versions did, so `mempack5` and `mempack6`
peers on v15 and v16 still talk to each other.

### Text Formats (Newtonsoft.Json)

These formats require the `ActualLab.Serialization.NewtonsoftJson` package. They are static
members of `RpcNewtonsoftJsonWireFormats`; add them with `f.AddNewtonsoftJson()`.

| Format | Key | Description |
|--------|-----|-------------|
| `NewtonsoftJsonV5` | `njson5` | Newtonsoft.Json with V4 arguments, V3 messages. **Not client-selectable by default** &mdash; see [Client-Selectable Formats](#client-selectable-formats) |
| `NewtonsoftJsonV5NP` | `njson5np` | Newtonsoft.Json, no-polymorphism variant. **Not client-selectable by default** |

### Binary Formats (Nerdbank.MessagePack)

These formats require the `ActualLab.Serialization.NerdbankMessagePack` package. They are
static members of `RpcNerdbankWireFormats`; add them with `f.AddNerdbankMessagePack()`.

| Format                  | Key          | Description                       |
|-------------------------|--------------|-----------------------------------|
| `NerdbankMessagePackV6` | `nmsgpack6`  | Nerdbank.MessagePack, V4 args, V5 messages |
| `NerdbankMessagePackV6C`| `nmsgpack6c` | Compact variant of V6             |

## Format Selection

### Default Format

The default is `MessagePackV6` (`msgpack6`). Before v16 it was `MemoryPackV6` (`mempack6`).

A client sends its default format key in the connection URL, so the client's default decides
which format a connection uses; a server only has to *serve* that format. Set the default per
container with `f.DefaultFormat`:

<!-- snippet: PartRSerialization_UseDefault -->
```cs
// Compact MessagePack with LZ4-compressed server-to-client frames
services.AddRpc().ConfigureWireFormats(f => {
    f.AddLZ4Variants();
    f.DefaultFormat = f["msgpack6c-lz4"];
});
```
<!-- endSnippet -->

A container that never calls `ConfigureWireFormats` serves `RpcDefaultWireFormats.All`, and
`RpcDefaultWireFormats.DefaultFormat` (`msgpack6`) is the default format of every container
that doesn't set `f.DefaultFormat`. Both are read-only: what a container serves is configured only
through `ConfigureWireFormats`.

### Client-Server Negotiation

When a client connects, it requests its preferred wire format via a URL parameter (e.g., `<endpoint>?f=msgpack6&clientId=...`). The server accepts the connection if it supports that format **and allows clients to select it**. Once connected, both parties simultaneously exchange `RpcHandshake` messages:

<img src="/img/diagrams/PartR-Serialization-1.svg" alt="Client-Server Negotiation" style="width: 100%; max-width: 800px;" />

### Accessing Formats

`RpcWireFormatResolver` is the registry a container actually uses. Resolve it from
DI to see what that container serves:

<!-- snippet: PartRSerialization_AccessingFormats -->
```cs
// The formats this container serves
var resolver = services.GetRequiredService<RpcWireFormatResolver>();
RpcWireFormat defaultFormat = resolver.DefaultFormat;
var format = resolver.Get("msgpack6c");

// The formats every container starts with (ActualLab.Rpc formats only)
ImmutableList<RpcWireFormat> builtIn = RpcDefaultWireFormats.All;
```
<!-- endSnippet -->

### Client-Selectable Formats

A registered format isn't automatically a format a *client* may pin via `?f=…`.
`RpcWireFormatResolver.BackendOnlyFormatKeys` lists the formats only backends may use:
every RPC server endpoint (WebSocket, HTTP/2, OWIN) rejects a connection whose requested
key is in it. An empty `f` still means "server picks", so it's always accepted.

**Since v14.2, `njson5` and `njson5np` are backend-only by default.** Newtonsoft-backed
formats deserialize with `TypeNameHandling.Auto` and no `SerializationBinder`, so they honor
nested `$type` markers &mdash; a gadget surface the other formats lack. Server-to-server and
in-process use of these formats is unaffected; only client-pinned selection is blocked.

The list is per container. To make more formats backend-only, e.g. the legacy V5 ones:

<!-- snippet: PartRSerialization_ClientSelection -->
```cs
services.AddRpc().ConfigureWireFormats(f
    => f.BackendOnlyFormatKeys = f.BackendOnlyFormatKeys.Union(["msgpack5", "msgpack5c"]));
```
<!-- endSnippet -->

To let clients select the Newtonsoft formats again, take them off the list:
`f.BackendOnlyFormatKeys = f.BackendOnlyFormatKeys.Except(["njson5", "njson5np"])`
(after `f.AddNewtonsoftJson()`).

`RpcDefaultWireFormats.BackendOnlyFormatKeys` seeds the list of every
`RpcWireFormatConfiguration` and resolver. It's read-only: change the list per container
through `RpcWireFormatConfiguration.BackendOnlyFormatKeys`.


## Format Structure

Each `RpcWireFormat` consists of:

<!-- snippet: PartRSerialization_FormatStructure -->
```cs
public sealed class RpcWireFormatExample(
    string key,
    Func<ArgumentListSerializer> argumentSerializerFactory,
    Func<RpcPeer, RpcMessageSerializer> messageSerializerFactory)
{
    public string Key { get; } = key;
    public ArgumentListSerializer ArgumentListSerializer { get; } = argumentSerializerFactory();
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


## Frame Compression

The `-lz4` and `-lz4f` variants compress whole frames &mdash; a frame is one batch of messages,
so a batch compresses as a unit and the LZ4 dictionary carries across frames.

Which directions are compressed is part of the format, not something the peers negotiate:
`-lz4` compresses server &rarr; client only, `-lz4f` compresses both. A client's outbound traffic
is mostly small call headers, where compression usually costs more than it saves, so `-lz4`
is the better default. Both peers derive the same answer from the format key, which they
already agree on when the connection is established, so there is nothing extra on the wire and
no way for the two to disagree.

Text formats have no compressed variants: a compressed frame is binary, and a text peer reads
text frames.

`RpcCompressionFormat` names the codec pair and carries the policy its compressor follows
(`RpcCompressionOptions`): the minimum frame size worth compressing, and how many frames or
bytes a compression context may span before it's reset. Resetting bounds both the memory a
connection pins and how far a BREACH-style probe can correlate across frames &mdash; it does
**not** make compressing a secret alongside attacker-controlled data within one frame safe.

Since v16 the LZ4 variants are not in the default set. `f.AddLZ4Variants()` adds the `-lz4`
(`RpcCompressionMode.ServerToClient`) variant of every uncompressed binary format added so far.
Pass compression modes to choose the variants, e.g. `RpcCompressionMode.Full` for `-lz4f`, and a
filter first to cover only some formats, e.g. `f.AddLZ4Variants(x => x.Key.StartsWith("msgpack6"))`. Text formats are always skipped, since they
can't be compressed. `RpcWireFormat.WithLZ4(...)` makes the LZ4 variant of a
single format. A client can use a `-lz4` format only if the server serves it, so add the variants
on both sides.

To compress with different settings, add a format of your own:

<!-- snippet: PartRSerialization_CustomLz4Format -->
```cs
var lz4 = new RpcCompressionFormat("lz4",
    static () => new LZ4ByteCompressor(),
    static () => new LZ4ByteDecompressor(),
    new RpcCompressionOptions { MinCompressedFrameSize = 1024 });
var format = new RpcWireFormat("msgpack6c-lz4-1k",
    () => new ByteArgumentListSerializer(MessagePackByteSerializer.Default),
    peer => new RpcByteMessageSerializerV5Compact(peer),
    DataFormat.Bytes,
    lz4, RpcCompressionMode.ServerToClient);
services.AddRpc().ConfigureWireFormats(f => f.Add(format));
```
<!-- endSnippet -->


## Configuring Formats

`services.AddRpc().ConfigureWireFormats(...)` configures the formats of one DI container:
it edits `RpcConfiguration.WireFormats` (also available as `rpc.WireFormats`), an `RpcWireFormatConfiguration` that starts from
`RpcDefaultWireFormats`. Every call on the same container edits the same set, and the
container's `RpcWireFormatResolver` is built from it on first use. The set is frozen
together with `RpcConfiguration` (when `RpcHub` is created) or when the resolver is built,
so it can't change after that.

| Member | What it does |
|----------------|--------------|
| `Add(params ReadOnlySpan<RpcWireFormat>)` | Adds formats; a format with the same key replaces the existing one (and becomes the default if the replaced one was) |
| `Remove(params ReadOnlySpan<string> keys)` / `Clear()` | Removes formats by key / removes all of them |
| `Reset()` | Restores the formats, the default format and the backend-only formats of `RpcDefaultWireFormats` |
| `AddLZ4Variants([filter], params modes)` | Adds the LZ4 variants of every uncompressed binary format the filter accepts (all of them if there's no filter): `-lz4` for `ServerToClient` (the default), `-lz4f` for `Full`; keys already present are kept |
| `DefaultFormat` | The default format (the one a client requests); it must be one of the added formats |
| `BackendOnlyFormatKeys` | The formats clients can't select |
| `AddMemoryPack()` | `mempack5`, `mempack5c`, `mempack6`, `mempack6c` (`ActualLab.Serialization.MemoryPack`) |
| `AddNewtonsoftJson()` | `njson5`, `njson5np` (`ActualLab.Serialization.NewtonsoftJson`) |
| `AddNerdbankMessagePack()` | `nmsgpack6`, `nmsgpack6c` (`ActualLab.Serialization.NerdbankMessagePack`) |

### Enabling Package-Provided Formats

<!-- snippet: PartRSerialization_AddFormats -->
```cs
services.AddRpc().ConfigureWireFormats(f => {
    f.AddMemoryPack();          // mempack5, mempack5c, mempack6, mempack6c
    f.AddNewtonsoftJson();      // njson5, njson5np
    f.AddNerdbankMessagePack(); // nmsgpack6, nmsgpack6c
    f.AddLZ4Variants(RpcCompressionMode.ServerToClient, RpcCompressionMode.Full); // "-lz4" and "-lz4f"
    f.DefaultFormat = f["mempack6c-lz4"];
});
```
<!-- endSnippet -->

Call `AddLZ4Variants()` after the `Add*` calls: it only sees the formats added before it.

### Registering Custom Formats

<!-- snippet: PartRSerialization_RegisterFormat -->
```cs
services.AddRpc().ConfigureWireFormats(f => f.Add(
    new RpcWireFormat(
        "custom",
        () => new MyArgumentListSerializer(),
        peer => new MyMessageSerializer(peer),
        DataFormat.Bytes)));
```
<!-- endSnippet -->

### Removing Formats

To disable older formats for security:

<!-- snippet: PartRSerialization_RemoveFormats -->
```cs
services.AddRpc().ConfigureWireFormats(f => f.Remove("msgpack5", "msgpack5c"));
```
<!-- endSnippet -->


## Format Selection Factors

When choosing formats, consider:

| Factor | Recommendation |
|--------|----------------|
| Performance | Binary formats (MessagePack, or MemoryPack from its package; both beat JSON) |
| Debugging | JSON formats (human-readable) |
| Compatibility | MessagePack: it's built in and works on every target, including .NET Framework |
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

This is determined by `ArgumentListSerializer.IsPolymorphic(Type)`:

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
(e.g., via `[JsonDerivedType]`, `[Union]`, or `[MemoryPackUnion]` if you use the MemoryPack formats),
the RPC layer's `TypeRef` wrapping is redundant overhead.
Apply `[RpcSerializable]` to the base type to tell RPC that
the type can be serialized directly:

<!-- snippet: PartRSerialization_RpcSerializableAttribute -->
```cs
// The underlying serializers handle polymorphism via union attributes,
// so we mark this type as RPC-serializable to opt out of TypeRef wrapping.
[RpcSerializable]
[MessagePackObject]
[Union(0, typeof(ShapeCircle))]
[Union(1, typeof(ShapeRect))]
[JsonDerivedType(typeof(ShapeCircle), "circle")]
[JsonDerivedType(typeof(ShapeRect), "rect")]
public abstract partial class Shape
{
    [DataMember, Key(0)]
    public string? Name { get; set; }
}

[DataContract, MessagePackObject]
public partial class ShapeCircle : Shape
{
    [DataMember, Key(1)]
    public double Radius { get; set; }
}

[DataContract, MessagePackObject]
public partial class ShapeRect : Shape
{
    [DataMember, Key(1)]
    public double Width { get; set; }

    [DataMember, Key(2)]
    public double Height { get; set; }
}
```
<!-- endSnippet -->

With this attribute, `ArgumentListSerializer.IsPolymorphic(typeof(Shape))` returns `false`,
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
