# Unified Serialization

ActualLab provides a unified serialization infrastructure used throughout Fusion, RPC, and the Operations Framework.
This guide covers the core serialization APIs, type-decorated serialization for polymorphism, lazy serialization
wrappers, and how to configure serialization globally.

## Required Package

| Package | Purpose |
|---------|---------|
| [ActualLab.Core](https://www.nuget.org/packages/ActualLab.Core/) | Core serialization infrastructure, System.Text.Json and MessagePack serializers |
| [ActualLab.Serialization.MemoryPack](https://www.nuget.org/packages/ActualLab.Serialization.MemoryPack/) | Optional: MemoryPack serializer, formatters for ActualLab types, `mempack*` RPC formats |
| [ActualLab.Serialization.NewtonsoftJson](https://www.nuget.org/packages/ActualLab.Serialization.NewtonsoftJson/) | Optional: Newtonsoft.Json serializer, its contract resolver and converters, `njson*` RPC formats |
| [ActualLab.Serialization.NerdbankMessagePack](https://www.nuget.org/packages/ActualLab.Serialization.NerdbankMessagePack/) | Optional: Nerdbank.MessagePack serializer and `nmsgpack*` RPC formats |

Since v16, `ActualLab.Core`, `ActualLab.Rpc`, `ActualLab.Fusion` and most other Fusion packages
don't depend on MemoryPack or Newtonsoft.Json. The exceptions are the packages that persist data
with Newtonsoft.Json (`ActualLab.Fusion.EntityFramework`, `ActualLab.Fusion.Ext.Contracts`,
`ActualLab.Fusion.Ext.Services`): they reference `ActualLab.Serialization.NewtonsoftJson`.
See [Upgrading to v16](#upgrading-to-v16) if you used either serializer.

## Overview

The serialization infrastructure provides:

- **Multiple format support**: System.Text.Json and MessagePack out of the box; Newtonsoft.Json,
  MemoryPack and Nerdbank.MessagePack via their packages
- **Type-decorated serialization**: Preserves type information for polymorphic deserialization
- **Lazy serialization wrappers**: Defer serialization/deserialization until needed
- **Multi-format types**: Single type that works with any serializer (for cross-format scenarios)


## Core Abstractions

### IByteSerializer

Binary serializers implement `IByteSerializer`:

<!-- snippet: PartS_IByteSerializer -->
```cs
public interface IByteSerializer
{
    object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength);
    void Write(IBufferWriter<byte> bufferWriter, object? value, Type type);
}
```
<!-- endSnippet -->

### ITextSerializer

Text (JSON) serializers implement `ITextSerializer`:

<!-- snippet: PartS_ITextSerializer -->
```cs
public interface ITextSerializer
{
    object? Read(string data, Type type);
    string Write(object? value, Type type);
}
```
<!-- endSnippet -->

Both interfaces have generic typed versions (`IByteSerializer<T>`, `ITextSerializer<T>`) for improved performance.


## Built-in Serializers

### Default Serializers

Global default serializers are accessible via static properties:

<!-- snippet: PartS_DefaultSerializers -->
```cs
// Binary serializer - MessagePack
IByteSerializer binary = ByteSerializer.Default;

// Text serializer - System.Text.Json
ITextSerializer text = TextSerializer.Default;
```
<!-- endSnippet -->

You can change defaults globally:

<!-- snippet: PartS_ChangeDefaultSerializer -->
```cs
// Use Newtonsoft.Json as default text serializer
// (requires ActualLab.Serialization.NewtonsoftJson)
TextSerializer.Default = NewtonsoftJsonSerializer.Default;
```
<!-- endSnippet -->

### Serializer Classes

| Class | Format | Type | Package | Notes |
|-------|--------|------|---------|-------|
| `MessagePackByteSerializer` | MessagePack | Binary | `ActualLab.Core` | Default binary serializer |
| `SystemJsonSerializer` | System.Text.Json | Text | `ActualLab.Core` | Default text serializer |
| `MemoryPackByteSerializer` | MemoryPack | Binary | `ActualLab.Serialization.MemoryPack` | No .NET Framework / .NET Standard 2.0 support |
| `NewtonsoftJsonSerializer` | Newtonsoft.Json | Text | `ActualLab.Serialization.NewtonsoftJson` | Flexible JSON with polymorphism support |

Before v16, `ByteSerializer.Default` was MemoryPack on .NET 6+; now it's MessagePack on every
target.

Each has a static `Default` property and options for customization:

<!-- snippet: PartS_SerializerInstances -->
```cs
// Access default instances
var msgPack = MessagePackByteSerializer.Default;
var sysJson = SystemJsonSerializer.Default;
var memPack = MemoryPackByteSerializer.Default;   // ActualLab.Serialization.MemoryPack
var newtonsoft = NewtonsoftJsonSerializer.Default; // ActualLab.Serialization.NewtonsoftJson

// Create with custom options
var prettyJson = new SystemJsonSerializer(new JsonSerializerOptions { WriteIndented = true });
```
<!-- endSnippet -->


## Type-Decorated Serialization

Type-decorated serializers embed type information in the serialized output, enabling polymorphic deserialization
when the exact type isn't known at compile time.

### TypeDecoratingTextSerializer

Embeds type info as a comment prefix in JSON:

<!-- snippet: PartS_TypeDecoratingTextSerializer -->
```cs
var serializer = TypeDecoratingTextSerializer.Default;

// Serialize
string json = serializer.Write(value, typeof(object));
// Output: /* @type MyNamespace.MyClass, MyAssembly */ {"name":"test"}

// Deserialize - type is recovered from the prefix
object? result = serializer.Read(json, typeof(object));
// result is MyClass
```
<!-- endSnippet -->

Format details:
- Prefix: `/* @type TypeName */ `
- When serialized type equals declared type, uses shorthand: `/* @type . */ `
- Assembly versions are stripped for forward compatibility

### TypeDecoratingByteSerializer

Prepends `TypeRef` to binary data:

<!-- snippet: PartS_TypeDecoratingByteSerializer -->
```cs
var serializer = TypeDecoratingByteSerializer.Default;

// Type info is binary-encoded before the payload
var buffer = serializer.Write(value, typeof(object));
object? result = serializer.Read(buffer.WrittenMemory, typeof(object), out _);
```
<!-- endSnippet -->

### Default Type-Decorated Instances

Each serializer class provides a type-decorated variant:

<!-- snippet: PartS_TypeDecoratedInstances -->
```cs
var sysJsonTD = SystemJsonSerializer.DefaultTypeDecorating;
var msgPackTD = MessagePackByteSerializer.DefaultTypeDecorating;
var newtonsoftTD = NewtonsoftJsonSerializer.DefaultTypeDecorating;
var memPackTD = MemoryPackByteSerializer.DefaultTypeDecorating;
```
<!-- endSnippet -->


## Serialized\<T> Wrappers

Lazy serialization wrappers defer (de)serialization until accessed. This is useful when:
- Data might not be needed (avoids unnecessary deserialization)
- Data needs to pass through multiple serialization boundaries
- Working with heterogeneous storage formats

### ByteSerialized\<T>

Wraps a value that serializes to/from bytes:

<!-- snippet: PartS_ByteSerializedMessage -->
```cs
[DataContract, MessagePackObject]
public partial record MyMessage(
    [property: DataMember(Order = 0), Key(0)] ByteSerialized<MyPayload> Payload);
```
<!-- endSnippet -->

<!-- snippet: PartS_ByteSerializedUsage -->
```cs
// Create with a value - serialization is deferred
var wrapper1 = ByteSerialized.New(myPayload);

// Or create from serialized data - deserialization is deferred
var wrapper2 = ByteSerialized.New<MyPayload>(bytes);

// Access triggers (de)serialization
MyPayload value = wrapper1.Value;
ReadOnlyMemory<byte> data = wrapper1.Data;
```
<!-- endSnippet -->

### TextSerialized\<T>

Same pattern for text/JSON:

<!-- snippet: PartS_TextSerializedUsage -->
```cs
var wrapper = TextSerialized.New(myObject);
string json = wrapper.Data;    // Serialize on access
MyObject value = wrapper.Value; // Deserialize on access
```
<!-- endSnippet -->

### Specialized Variants

| Type | Serializer | Package |
|------|------------|---------|
| `MessagePackSerialized<T>` | MessagePack binary | `ActualLab.Core` |
| `SystemJsonSerialized<T>` | System.Text.Json | `ActualLab.Core` |
| `MemoryPackSerialized<T>` | MemoryPack binary | `ActualLab.Serialization.MemoryPack` |
| `NewtonsoftJsonSerialized<T>` | Newtonsoft.Json | `ActualLab.Serialization.NewtonsoftJson` |

The type-decorating variants (`TypeDecoratingMessagePackSerialized<TSchema, T>`,
`TypeDecoratingMemoryPackSerialized<TSchema, T>`, etc.) live next to them.


## Multi-Format Types: UniSerialized\<T>

`UniSerialized<T>` works with every serialization format. Its converters serialize `Value` with the
serializer that serializes the `UniSerialized<T>` itself, so the nested payload uses the same options
(e.g. the same MessagePack resolver). Every serializer exposes itself as `ByteSerializer.Current` while
its `Read` or `Write` runs. A converter requires it to be the serializer of the converter's own format and throws
otherwise, so `UniSerialized<T>` can be serialized only through an ActualLab serializer:

<!-- snippet: PartS_UniSerializedStructure -->
```cs
[JsonConverter(typeof(UniSerializedJsonConverter))]
[MessagePackFormatter(typeof(UniSerializedMessagePackFormatter<>))]
public readonly struct UniSerialized<T>
{
    public T Value { get; init; }
}

// Every format's converter writes Value as a nested payload produced by the serializer
// that serializes the UniSerialized<T> itself (ByteSerializer.Current), wrapped as:
// - System.Text.Json:      { "json": "..." }
// - Newtonsoft.Json:       { "NewtonsoftJson": "..." }
// - MessagePack, Nerdbank: [ bin ]
// - MemoryPack:            a version-tolerant object with a single byte array member
```
<!-- endSnippet -->

This design allows the same data structure to work correctly regardless of which serializer is used
on each side of the communication. The wire shapes are the same as before v16, except for
Nerdbank.MessagePack: it now writes `[bin]`, like MessagePack, instead of `{"MessagePack": bin}`.

### Unscoped Serializer API

Serializer interfaces declare only `ReadUnscoped` and `WriteUnscoped` (plus `Root` and `ToTyped`):
these don't activate anything. `Read` and `Write` are extension methods that open a `ByteSerializer.Scope`,
i.e. activate `serializer.Root` as `ByteSerializer.Current`, around the unscoped call. `Root` is the untyped
serializer that typed serializers and wrappers like `TypeDecoratingByteSerializer` expose to converters.
Code making a series of calls with the same serializer opens the scope once (`serializer.Activate()`,
or `new ByteSerializer.Scope(serializer.Root)`) and calls the unscoped methods.
RPC argument list serializers do this to serialize all arguments of a call in one scope:

```cs
using (serializer.Activate()) {
    serializer.WriteUnscoped(buffer, first, typeof(string));
    serializer.WriteUnscoped(buffer, second, typeof(PropertyBag));
}
```

These are low-level methods: an unscoped call made outside the scope fails as soon as it reaches
a `UniSerialized<T>` value.

### TypeDecoratingUniSerialized\<T>

Adds type decoration for polymorphism:

<!-- snippet: PartS_TypeDecoratingUniSerialized -->
```cs
// Used by PropertyBag for storing heterogeneous values
var item = TypeDecoratingUniSerialized.New<TypeSchema.Any, object>(myValue);
```
<!-- endSnippet -->


## PropertyBag Serialization

`PropertyBag` stores key-value pairs where values can be any type. It uses
`TypeDecoratingUniSerialized<TSchema, object>` to preserve type information:

<!-- snippet: PartS_PropertyBagItem -->
```cs
// Internal structure of PropertyBagItem
[DataContract]
public partial record struct PropertyBagItem<TSchema>(
    [property: DataMember(Order = 0), Key(0)] string Key,
    [property: DataMember(Order = 1), Key(1)] TypeDecoratingUniSerialized<TSchema, object> Serialized)
    where TSchema : TypeSchema, new();
```
<!-- endSnippet -->

This allows:
- Heterogeneous value types in the same bag
- Cross-format serialization (JSON to binary and back)
- Type preservation across serialization boundaries

::: tip Operations Framework
For details on how PropertyBag is used in the Operations Framework, see [Operations Framework Serialization](./PartO-Serialization.md).
:::


## Annotating Types for Serialization

The baseline set of attributes covers System.Text.Json, MessagePack and Newtonsoft.Json:

<!-- snippet: PartS_AnnotatedRecord -->
```cs
[DataContract, MessagePackObject]
[method: System.Text.Json.Serialization.JsonConstructor, SerializationConstructor]
public sealed partial record MyRecord(
    [property: DataMember(Order = 0), Key(0)] string Name,
    [property: DataMember(Order = 1), Key(1)] int Value);
```
<!-- endSnippet -->

| Attribute | Purpose |
|-----------|---------|
| `[DataContract]` | Newtonsoft.Json and other `DataContract`-aware serializers |
| `[MessagePackObject]` | MessagePack; `[MessagePackObject(true)]` uses string keys instead of `[Key(n)]` |
| `[DataMember(Order = n)]` | Member order for `DataContract`-aware serializers; keep `n` equal to the `Key` |
| `[Key(n)]` | MessagePack member key |
| `[JsonConstructor]` | System.Text.Json (and Newtonsoft.Json) constructor |
| `[SerializationConstructor]` | MessagePack constructor |

If you use MemoryPack (the `ActualLab.Serialization.MemoryPack` package and the `mempack*` RPC
formats), add its attributes too:

<!-- snippet: PartS_AnnotatedRecordMemoryPack -->
```cs
// Only if you use MemoryPack (ActualLab.Serialization.MemoryPack)
[DataContract, MessagePackObject, MemoryPackable(GenerateType.VersionTolerant)]
[method: System.Text.Json.Serialization.JsonConstructor, SerializationConstructor, MemoryPackConstructor]
public sealed partial record MyMemoryPackRecord(
    [property: DataMember(Order = 0), Key(0), MemoryPackOrder(0)] string Name,
    [property: DataMember(Order = 1), Key(1), MemoryPackOrder(1)] int Value);
```
<!-- endSnippet -->

| Attribute | Purpose |
|-----------|---------|
| `[MemoryPackable(...)]` | MemoryPack source generator |
| `[MemoryPackOrder(n)]` | Member order for MemoryPack |
| `[MemoryPackConstructor]` | MemoryPack constructor |
| `[MemoryPackAllowSerialize]` | Required on members of ActualLab types &ndash; see [Upgrading to v16](#upgrading-to-v16) |


## SerializerKind Enum

For code that needs to select serializers dynamically. `SerializerKind` lists `MemoryPack`,
`NewtonsoftJson` and `NerdbankMessagePack` too, but they resolve through a registry their packages fill:
`GetDefaultSerializer()` throws for a kind whose package isn't registered, and `IsAvailable()`
tells whether it is. `Custom0`..`Custom7` (`0x10`..`0x17`) are free for your own serializers:
register one with `SerializerKind.Custom0.Register(() => serializer, () => typeDecoratingSerializer)`.

<!-- snippet: PartS_SerializerKindUsage -->
```cs
// Get default serializer for a kind
IByteSerializer serializer = SerializerKind.MessagePack.GetDefaultSerializer();

// Get type-decorated variant
IByteSerializer tdSerializer = SerializerKind.MessagePack.GetDefaultTypeDecoratingSerializer();

// MemoryPack and NewtonsoftJson are available once their package registers them
if (SerializerKind.MemoryPack.IsAvailable())
    serializer = SerializerKind.MemoryPack.GetDefaultSerializer();
```
<!-- endSnippet -->


## Configuration

### Global Default Changes

<!-- snippet: PartS_GlobalConfiguration -->
```cs
// Change default binary serializer
ByteSerializer.Default = MessagePackByteSerializer.Default;

// Change default text serializer (requires ActualLab.Serialization.NewtonsoftJson)
TextSerializer.Default = NewtonsoftJsonSerializer.Default;

// Change System.Text.Json options
SystemJsonSerializer.DefaultOptions = new JsonSerializerOptions {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
};

// Change Newtonsoft.Json settings
NewtonsoftJsonSerializer.DefaultSettings = new JsonSerializerSettings {
    TypeNameHandling = TypeNameHandling.Auto,
    NullValueHandling = NullValueHandling.Ignore,
    // Keep it: ActualLab types carry no Newtonsoft.Json attributes since v16
    ContractResolver = new NewtonsoftJsonContractResolver(),
};
```
<!-- endSnippet -->


## Best Practices

1. **Use consistent annotations**: Apply all serialization attributes to types that may cross serialization boundaries
2. **Provide parameterless constructors**: Most serializers require them; use the appropriate constructor attributes
3. **Test cross-format scenarios**: If data might be serialized with one format and deserialized with another, test explicitly
4. **Use TypeDecorating for polymorphism**: When storing/sending base types that may contain derived instances
5. **Consider version tolerance**: With MessagePack, add members with new `Key` values and never reuse a key;
   with MemoryPack, use `MemoryPackable(GenerateType.VersionTolerant)` for types that may evolve


## Upgrading to v16

v16 moves MemoryPack and Newtonsoft.Json support out of `ActualLab.Core` into separate packages.
The wire formats themselves don't change: `mempack*`, `njson*` and the persisted Newtonsoft.Json
data are byte-for-byte the same as in v15. What changes is the defaults and how you opt in.

### 1. Reference the packages you use

| If you use | Reference |
|------------|-----------|
| `MemoryPackByteSerializer`, `MemoryPackSerialized<T>`, `[MemoryPackable]` types with ActualLab members, `mempack*` RPC formats | `ActualLab.Serialization.MemoryPack` |
| `NewtonsoftJsonSerializer`, `NewtonsoftJsonSerialized<T>`, `njson*` RPC formats | `ActualLab.Serialization.NewtonsoftJson` |
| `nmsgpack*` RPC formats | `ActualLab.Serialization.NerdbankMessagePack` |
| `OptionSet`, `ImmutableOptionSet` (obsolete) | `ActualLab.Core.Legacy` |
| `IAuthBackend`, `IKeyValueStore` and their commands without `ActualLab.Fusion.Ext.Services` | `ActualLab.Fusion.Ext.Backend.Contracts` |

`ActualLab.Serialization.MemoryPack` targets .NET Core 3.1+ and .NET Standard 2.1; it has no
.NET Framework or .NET Standard 2.0 build.

The packages register their formatters, converters and `SerializerKind` entries from a module
initializer when their assembly loads (on .NET 5+). If nothing in your app touches the package
before the first serialization (e.g. you only use `[MemoryPackable]` types), or you target
.NET Core 3.1 / .NET Standard 2.1, call `MemoryPackSerialization.Register()` /
`NewtonsoftJsonSerialization.Register()` at startup.

`IAuthBackend`, `IKeyValueStore` and their commands moved to `ActualLab.Fusion.Ext.Backend.Contracts`;
`ActualLab.Fusion.Ext.Services` type-forwards them, so code compiles unchanged. Their assembly
name is part of the type name they're serialized with, so upgrade all hosts that exchange these
commands together.

### 2. Fix MEMPACK019 in your `[MemoryPackable]` types

ActualLab types no longer carry `[MemoryPackable]`; the package serializes them with hand-written
formatters registered at runtime. The MemoryPack source generator can't see those formatters, so
a `[MemoryPackable]` type with a member like `Session`, `Symbol`, `TypeRef`, `PropertyBag`,
`Option<string>`, `Result<T>` or `User` fails to compile with **MEMPACK019**. Add
`[MemoryPackAllowSerialize]` to each such member:

<!-- snippet: PartS_MemoryPackAllowSerialize -->
```cs
[DataContract, MessagePackObject, MemoryPackable(GenerateType.VersionTolerant)]
[method: System.Text.Json.Serialization.JsonConstructor, SerializationConstructor, MemoryPackConstructor]
public sealed partial record Note(
    // ActualLab types: [MemoryPackAllowSerialize] is required since v16
    [property: DataMember(Order = 0), Key(0), MemoryPackOrder(0), MemoryPackAllowSerialize] Session Session,
    [property: DataMember(Order = 1), Key(1), MemoryPackOrder(1), MemoryPackAllowSerialize] Symbol Tag,
    [property: DataMember(Order = 2), Key(2), MemoryPackOrder(2), MemoryPackAllowSerialize] Option<string> Title,
    // Not needed for ApiArray/ApiList/ApiSet/ApiMap, unmanaged structs
    // and Option/ApiOption/ApiNullable over an unmanaged T
    [property: DataMember(Order = 3), Key(3), MemoryPackOrder(3)] ApiArray<string> Lines,
    [property: DataMember(Order = 4), Key(4), MemoryPackOrder(4)] Option<int> Priority);
```
<!-- endSnippet -->

You don't need it for `ApiArray<T>`, `ApiList<T>`, `ApiSet<T>`, `ApiMap<TKey, TValue>`, unmanaged
structs (e.g. `Moment`, `CpuTimestamp`), and `Option<T>` / `ApiOption<T>` / `ApiNullable<T>` over an
unmanaged `T`. Rebuild every assembly that declares such types: code the generator produced for
v15 calls MemoryPack members ActualLab types no longer have.

If a `[MemoryPackable]` type of yours nests a generic ActualLab type inside another generic
(e.g. a `Dictionary<string, Option<MyStruct>>` member), call `MemoryPackSerialization.Register<T>()`
for that type at startup, so the nested type is serialized the same way as in v15.

### 3. Drop MemoryPack and Newtonsoft.Json attributes you don't need

Your API records no longer need MemoryPack or Newtonsoft.Json attributes unless you use those
serializers: `[DataContract]`, MessagePack's `[MessagePackObject]` / `[Key]` and the System.Text.Json
attributes are the baseline (see [Annotating Types](#annotating-types-for-serialization)).

### 4. Configure RPC formats per container

The default RPC format is now `msgpack6` (was `mempack6`), and the default format set is slim:
`json5`, `json5np`, `msgpack5`, `msgpack5c`, `msgpack6`, `msgpack6c`. Everything else &ndash;
including the `-lz4` variants &ndash; is opt-in via `RpcBuilder.ConfigureWireFormats`:

<!-- snippet: PartS_UpgradeRpcFormats -->
```cs
services.AddRpc().ConfigureWireFormats(f => {
    f.AddMemoryPack();     // ActualLab.Serialization.MemoryPack: mempack5(c), mempack6(c)
    f.AddNewtonsoftJson(); // ActualLab.Serialization.NewtonsoftJson: njson5, njson5np
    f.AddLZ4Variants(); // "-lz4" variants of the formats added so far
    f.DefaultFormat = f["msgpack6c-lz4"];
});
```
<!-- endSnippet -->

- A client that pins a format (e.g. `f.DefaultFormat = f["msgpack6c-lz4"]`) needs a server that serves it,
  so add the same formats on the server.
- To keep v15 clients (which default to `mempack6`) working during a rolling upgrade, the server
  must serve `mempack6`: add `f.AddMemoryPack()` there.
- `RpcNerdbankSerializationFormat.Register()` is gone; use `f.AddNerdbankMessagePack()`.
- RPC formats are now "wire formats": `RpcSerializationFormat` became `RpcWireFormat`,
  `RpcSerializationFormatResolver` became `RpcWireFormatResolver`, and `RpcHub.SerializationFormats`,
  `RpcPeer.SerializationFormat` and `RpcRef.SerializationFormat` became `WireFormats` / `WireFormat`.
- The built-in formats (`SystemJsonV5`, `MessagePackV6`, ...) moved from `RpcSerializationFormat` to
  `RpcDefaultWireFormats`; the package ones are in `RpcMemoryPackWireFormats`,
  `RpcNewtonsoftJsonWireFormats` and `RpcNerdbankWireFormats`.
- `RpcSerializationFormat.All`, `RpcSerializationFormat.Default`, `RpcSerializationFormatResolver.DefaultFormats`
  and the `*_LZ4` / `*_LZ4F` format statics are gone. Configure each container with
  `rpc.ConfigureWireFormats(f => ...)` instead: `f.Add(...)` / `f.Remove(...)`, `f.AddLZ4Variants()`,
  `f.DefaultFormat` (instead of assigning `RpcSerializationFormatResolver.Default`) and
  `f.BackendOnlyFormatKeys` (instead of `RpcSerializationFormatResolver.DefaultClientDeniedFormatKeys`;
  the process-wide defaults are the read-only `RpcDefaultWireFormats.DefaultFormat` and `.BackendOnlyFormatKeys`).

See [RPC Wire Formats](./PartR-Serialization.md#configuring-formats) for the builder API.

### 5. Custom Newtonsoft.Json settings need `NewtonsoftJsonContractResolver`

ActualLab types no longer carry Newtonsoft.Json attributes or `[JsonConverter]`s. The default
`NewtonsoftJsonSerializer.DefaultSettings` use `NewtonsoftJsonContractResolver`, which reads their
System.Text.Json and `[DataContract]` attributes and supplies their converters. If you create
`JsonSerializerSettings` yourself, set `ContractResolver = new NewtonsoftJsonContractResolver()` (or a
subclass of it) &ndash; with the stock `DefaultContractResolver`, types like `Symbol`, `Moment` or
`Session` serialize differently.

Persisted data written by Fusion (the EF operation log, the key-value store and auth data of
`ActualLab.Fusion.Ext.Services`) still uses Newtonsoft.Json, in the same format as before; these
packages reference `ActualLab.Serialization.NewtonsoftJson` themselves.

### 6. `Ulid` needs its own MessagePack resolver

`ActualLab.Core` no longer references the `Ulid` and `Ulid.MessagePack` packages, and its MessagePack
resolver no longer knows `Ulid`. `UlidUuidGenerator` still produces ULID strings without them.

- Code that used `Ulid` through Core's reference must now reference the `Ulid` package itself.
- To send `Ulid` with MessagePack (the default RPC format is `msgpack6`), reference `Ulid.MessagePack`
  and prepend its resolver at startup, before the first `Ulid` is serialized &ndash;
  `DefaultMessagePackResolver` caches the formatter of each type on first use:

```cs
// using ActualLab.Serialization.Internal; using Cysharp.Serialization.MessagePack;
DefaultMessagePackResolver.Resolvers = [UlidMessagePackResolver.Instance, .. DefaultMessagePackResolver.Resolvers];
```

  Without it, the first call that carries a `Ulid` fails with a MessagePack serialization error.
