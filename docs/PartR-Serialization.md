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
| `SystemJsonV3` | `json3` | System.Text.Json with V3 argument/message serialization |
| `SystemJsonV5` | `json5` | System.Text.Json with V4 arguments, V3 messages |
| `NewtonsoftJsonV3` | `njson3` | Newtonsoft.Json with V3 argument/message serialization |
| `NewtonsoftJsonV5` | `njson5` | Newtonsoft.Json with V4 arguments, V3 messages |

### Binary Formats (MemoryPack)

| Format | Key | Description |
|--------|-----|-------------|
| `MemoryPackV1` | `mempack1` | Legacy V1 format |
| `MemoryPackV2` | `mempack2` | V2 args with forced polymorphism |
| `MemoryPackV2C` | `mempack2c` | Compact variant of V2 |
| `MemoryPackV3` | `mempack3` | V3 arguments |
| `MemoryPackV3C` | `mempack3c` | Compact variant of V3 |
| `MemoryPackV4` | `mempack4` | V3 args, V4 messages |
| `MemoryPackV4C` | `mempack4c` | Compact variant of V4 |
| `MemoryPackV5` | `mempack5` | V4 args, V4 messages |
| `MemoryPackV5C` | `mempack5c` | Compact variant of V5 |

### Binary Formats (MessagePack)

| Format | Key | Description |
|--------|-----|-------------|
| `MessagePackV1` | `msgpack1` | Legacy V1 format |
| `MessagePackV2` | `msgpack2` | V2 args with forced polymorphism |
| `MessagePackV2C` | `msgpack2c` | Compact variant of V2 |
| `MessagePackV3` | `msgpack3` | V3 arguments |
| `MessagePackV3C` | `msgpack3c` | Compact variant of V3 |
| `MessagePackV4` | `msgpack4` | V3 args, V4 messages |
| `MessagePackV4C` | `msgpack4c` | Compact variant of V4 |
| `MessagePackV5` | `msgpack5` | V4 args, V4 messages |
| `MessagePackV5C` | `msgpack5c` | Compact variant of V5 |

### Non-Polymorphic Variants

Some formats have `-np` (non-polymorphic) variants that skip type decoration:

| Format | Key |
|--------|-----|
| `MemoryPackV2NP` | `mempack2-np` |
| `MemoryPackV2CNP` | `mempack2c-np` |
| `MessagePackV2NP` | `msgpack2-np` |
| `MessagePackV2CNP` | `msgpack2c-np` |


## Format Selection

### Default Format

The default format is typically `MemoryPackV5C` (or latest version) for .NET 6+ and `MessagePackV5C` for .NET Standard.

### Client-Server Negotiation

When a client connects, it sends its supported formats. The server selects the best matching format:

```mermaid
sequenceDiagram
    participant C as Client
    participant S as Server
    C->>S: Hello (supported formats: mempack5c, mempack4c, mempack3c)
    S->>C: Hello (selected: mempack5c)
    Note over C,S: All subsequent messages use mempack5c
```

### Accessing All Formats

<!-- snippet: PartRSerialization_AccessingFormats -->
```cs
// All registered formats
ImmutableList<RpcSerializationFormat> all = RpcSerializationFormat.All;

// Find by key
var format = RpcSerializationFormat.All.First(f => f.Key == "mempack5c");
```
<!-- endSnippet -->


## Format Structure

Each `RpcSerializationFormat` consists of:

<!-- snippet: PartRSerialization_FormatStructure -->
```cs
public sealed class RpcSerializationFormatExample(
    string key,
    Func<RpcArgumentSerializer> argumentSerializerFactory,
    Func<RpcPeer, IByteSerializer<RpcMessage>> messageSerializerFactory)
{
    public string Key { get; } = key;
    public RpcArgumentSerializer ArgumentSerializer { get; } = argumentSerializerFactory();
    public Func<RpcPeer, IByteSerializer<RpcMessage>> MessageSerializerFactory { get; } = messageSerializerFactory;
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
| V1 | Original format, forced polymorphism |
| V2 | Improved with optional polymorphism |
| V3 | Optimized encoding |
| V4 | Latest, best performance |

### Message Serializer Versions

| Version | Variants | Description |
|---------|----------|-------------|
| V3 | Normal, Compact | Standard message framing |
| V4 | Normal, Compact | Improved framing |

### Compact vs Normal

Compact variants (`*C` suffix) use smaller message framing at a slight CPU cost. Choose compact for:
- Lower bandwidth scenarios
- When message overhead is significant relative to payload


## Configuring Formats

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
    .RemoveAll(f => f.Key.StartsWith("mempack1") || f.Key.StartsWith("msgpack1"));
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
| Security | Latest versions, disable V1 |


## Serialization in RPC Pipeline

```mermaid
flowchart LR
    subgraph Client
        C1[Method Call] --> C2[ArgumentSerializer]
        C2 --> C3[MessageSerializer]
        C3 --> C4[WebSocket]
    end
    C4 --> S4
    subgraph Server
        S4[WebSocket] --> S3[MessageSerializer]
        S3 --> S2[ArgumentSerializer]
        S2 --> S1[Method Invoke]
    end
```

1. Client serializes method arguments using `ArgumentSerializer`
2. Arguments are wrapped in an `RpcMessage` and serialized by `MessageSerializer`
3. Binary data is sent over WebSocket
4. Server deserializes in reverse order


## Related Topics

- [Core Serialization](./PartS.md) - General serialization infrastructure
- [RPC Key Concepts](./PartR-CC.md) - RPC architecture overview
- [Configuration Options](./PartR-CO.md) - RPC configuration
