# Unified Serialization

ActualLab provides a unified serialization infrastructure used throughout Fusion, RPC, and the Operations Framework.
This guide covers the core serialization APIs, type-decorated serialization for polymorphism, lazy serialization
wrappers, and how to configure serialization globally.

## Required Package

| Package | Purpose |
|---------|---------|
| [ActualLab.Core](https://www.nuget.org/packages/ActualLab.Core/) | Core serialization infrastructure, System.Text.Json and MessagePack serializers |

Newtonsoft.Json, Nerdbank.MessagePack and MemoryPack come from optional packages &ndash; see
[Serializer Packages](#serializer-packages).

## Overview

The serialization infrastructure provides:

- **Multiple format support**: System.Text.Json and MessagePack out of the box; Newtonsoft.Json,
  Nerdbank.MessagePack and MemoryPack via [their packages](#serializer-packages)
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
    IByteSerializer Root { get; }
    object? ReadUnscoped(ReadOnlyMemory<byte> data, Type type, out int readLength);
    void WriteUnscoped(IBufferWriter<byte> bufferWriter, object? value, Type type);
    IByteSerializer<T> ToTyped<T>(Type? serializedType = null);
}
```
<!-- endSnippet -->

### ITextSerializer

Text (JSON) serializers implement `ITextSerializer`:

<!-- snippet: PartS_ITextSerializer -->
```cs
public interface ITextSerializer : IByteSerializer
{
    object? ReadUnscoped(string data, Type type);
    string WriteUnscoped(object? value, Type type);
    // + ReadOnlyMemory<char> / TextWriter overloads
}
```
<!-- endSnippet -->

Both interfaces have generic typed versions (`IByteSerializer<T>`, `ITextSerializer<T>`) for improved performance.
Call the `Read` / `Write` extension methods rather than the `*Unscoped` members &ndash; see
[Unscoped Serializer API](#unscoped-serializer-api).


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
// Use System.Text.Json with custom options as the default text serializer
TextSerializer.Default = new SystemJsonSerializer(new JsonSerializerOptions { WriteIndented = true });
```
<!-- endSnippet -->

### Serializer Classes

| Class | Format | Type | Package | Notes |
|-------|--------|------|---------|-------|
| `MessagePackByteSerializer` | MessagePack | Binary | `ActualLab.Core` | Default binary serializer |
| `SystemJsonSerializer` | System.Text.Json | Text | `ActualLab.Core` | Default text serializer |

The serializer packages add `NewtonsoftJsonSerializer`, `NerdbankMessagePackByteSerializer` and
`MemoryPackByteSerializer` &ndash; see [Serializer Packages](#serializer-packages).

Each has a static `Default` property and options for customization:

<!-- snippet: PartS_SerializerInstances -->
```cs
// Access default instances
var msgPack = MessagePackByteSerializer.Default;
var sysJson = SystemJsonSerializer.Default;

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

The type-decorating variants (`TypeDecoratingMessagePackSerialized<TSchema, T>`,
`TypeDecoratingSystemJsonSerialized<TSchema, T>`) live next to them; the serializer packages add
their own (`NewtonsoftJsonSerialized<T>`, `MemoryPackSerialized<T>`, ...).


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
// - MessagePack, Nerdbank: [ bin ]
// The serializer packages (Newtonsoft.Json, MemoryPack) define their own shapes.
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
    where TSchema : TypeSchema;
```
<!-- endSnippet -->

This allows:
- Heterogeneous value types in the same bag
- Cross-format serialization (JSON to binary and back)
- Type preservation across serialization boundaries

`TSchema` restricts the types a type-decorating serializer may materialize: `TypeSchema.Any`
allows everything, `TypeSchema.PrimitiveOnly` only primitives, and you can derive your own. A custom
schema needs a public parameterless constructor, and in trimmed apps it has to keep it explicitly,
since it's created by reflection:

<!-- snippet: PartS_CustomTypeSchema -->
```cs
public sealed class MySchema : TypeSchema
{
    static MySchema() => CodeKeeper.Keep<MySchema>(); // Keeps the constructor in trimmed apps

    public override bool IsAllowed(Type type) => type.Namespace == "MyApp.Contracts";
}
```
<!-- endSnippet -->

::: tip Operations Framework
For details on how PropertyBag is used in the Operations Framework, see [Operations Framework Serialization](./PartO-Serialization.md).
:::


## Annotating Types for Serialization

The baseline set of attributes covers System.Text.Json and MessagePack:

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
| `[DataContract]` | `DataContract`-aware serializers |
| `[MessagePackObject]` | MessagePack; `[MessagePackObject(true)]` uses string keys instead of `[Key(n)]` |
| `[DataMember(Order = n)]` | Member order for `DataContract`-aware serializers; keep `n` equal to the `Key` |
| `[Key(n)]` | MessagePack member key |
| `[JsonConstructor]` | System.Text.Json constructor |
| `[SerializationConstructor]` | MessagePack constructor |

If you use MemoryPack, annotate your types with its attributes as well &ndash; see
[MemoryPack](#memorypack).


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

// Kinds served by a serializer package are available once that package registers them
if (SerializerKind.NerdbankMessagePack.IsAvailable())
    serializer = SerializerKind.NerdbankMessagePack.GetDefaultSerializer();
```
<!-- endSnippet -->


## Configuration

### Global Default Changes

<!-- snippet: PartS_GlobalConfiguration -->
```cs
// Change default binary serializer
ByteSerializer.Default = MessagePackByteSerializer.Default;

// Change System.Text.Json options
SystemJsonSerializer.DefaultOptions = new JsonSerializerOptions {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
};
```
<!-- endSnippet -->


## Serializer Packages

`ActualLab.Core` ships System.Text.Json and MessagePack. Other serializers plug in via separate
packages; each one registers its formatters, converters and `SerializerKind` entry from a module
initializer when its assembly loads (on .NET 5+). If nothing in your app touches the package
before the first serialization, or you target .NET Core 3.1 / .NET Standard 2.1, call its
`Register()` method at startup.

| Package | Serializer | RPC formats | Targets |
|---------|------------|-------------|---------|
| `ActualLab.Serialization.NewtonsoftJson` | `NewtonsoftJsonSerializer` | `njson5`, `njson5np` | All |
| `ActualLab.Serialization.NerdbankMessagePack` | `NerdbankMessagePackByteSerializer` | `nmsgpack6`, `nmsgpack6c` | .NET 8+ |
| `ActualLab.Serialization.MemoryPack` | `MemoryPackByteSerializer` | `mempack5(c)`, `mempack6(c)` | .NET Core 3.1+, .NET Standard 2.1 |

The RPC formats of a package are opt-in per container &ndash; see
[RPC Wire Formats](./PartR-Serialization.md#configuring-formats).

### Newtonsoft.Json

`ActualLab.Serialization.NewtonsoftJson` provides `NewtonsoftJsonSerializer`,
`NewtonsoftJsonSerialized<T>` and `NewtonsoftJsonContractResolver`. ActualLab types carry no
Newtonsoft.Json attributes; the contract resolver reads their System.Text.Json and `[DataContract]`
attributes and supplies their converters, so set it in any `JsonSerializerSettings` you create.

The packages that persist data with Newtonsoft.Json reference it themselves:
`ActualLab.Fusion.EntityFramework` (the operation log) and `ActualLab.Fusion.Ext.Services`.
The `njson*` RPC formats are backend-only by default: they honor nested `"$type"` markers,
which a public endpoint shouldn't accept.

### Nerdbank.MessagePack

`ActualLab.Serialization.NerdbankMessagePack` provides `NerdbankMessagePackByteSerializer` and
`NerdbankMessagePackSerialized<T>`, plus converters for ActualLab types. It targets .NET 8+.

::: info Experimental
Nerdbank.MessagePack support is experimental: it may change in minor versions, and
`ActualLab.Generators` doesn't generate its NativeAOT roots &ndash; the `NativeAotFull` sample shows
what to add by hand.
:::

### MemoryPack

`ActualLab.Serialization.MemoryPack` provides `MemoryPackByteSerializer`, `MemoryPackSerialized<T>`
and formatters for ActualLab types. Fusion versions before v16 used MemoryPack by default, and the
package produces the same bytes they did, so `mempack*` peers on v15 and v16 still talk to each other.

::: warning We don't recommend MemoryPack
Use MessagePack or System.Text.Json instead, unless you need wire compatibility with pre-v16 peers.
MemoryPack support is being retired: it's kept for compatibility, but gets no new features (e.g.
`ActualLab.Generators` doesn't generate its NativeAOT roots).
MemoryPack serializes unmanaged structs as raw memory, which is both a security issue and a
correctness one &ndash; see [MemoryPack and Unmanaged Structs](#memorypack-and-unmanaged-structs).
:::

A `[MemoryPackable]` type needs `[MemoryPackAllowSerialize]` on its members of ActualLab types
(e.g. `Session`, `Symbol`, `PropertyBag`, `Result<T>`): their formatters are registered at runtime,
and MemoryPack's source generator rejects member types it can't see a `[MemoryPackable]` annotation
on (**MEMPACK019**) &ndash; see
[Migrating to v16](./Migration/To-v16.md#step-2-fix-mempack019-in-your-memorypackable-types).

#### MemoryPack and Unmanaged Structs

MemoryPack copies an unmanaged struct &ndash; e.g. an `Option<int>`, an `int?` or a struct of
yours &ndash; as raw memory, padding bytes included. Padding holds whatever was in that memory
before, so:

- **It leaks memory contents.** Fragments of unrelated data end up in the serialized output.
- **Equal values serialize to different bytes.** Anything that compares or hashes serialized
  results misbehaves &ndash; e.g. the client-side computed cache treats an unchanged result as
  changed.

`ActualLab.Serialization.MemoryPack` provides alternatives for MemoryPack contracts that add no
padding of their own (a `T` with padding inside still leaks it):

| Instead of | Use | Why it's padding-free |
|------------|-----|-----------------------|
| `Option<T>` | `ApiOption<T>` | Packed (`Pack = 1`) |
| `T?` | `ApiNullable<T>` | Packed (`Pack = 1`) |
| `T?` with an 8-byte-aligned `T` (e.g. `long`, `Moment`) | `ApiNullable8<T>` | A `long` flag in place of `T?`'s `bool` and padding |

`ApiNullable8<T>` keeps the `T?` layout on purpose, but only for a `T` with 8-byte alignment
(e.g. `long`, `double`, `Moment`): an 8-byte struct of two `int` fields is 4-byte aligned, so its
`T?` puts the value at offset 4, while `ApiNullable8<T>` always puts it at 8. It reads any non-zero flag as "has value", so a `T?` payload
written with garbage in its padding can read back as non-null.
Your own unmanaged structs need the same care: avoid gaps between their fields, or give them
`[StructLayout(LayoutKind.Sequential, Pack = 1)]`.


## Best Practices

1. **Use consistent annotations**: Apply all serialization attributes to types that may cross serialization boundaries
2. **Provide parameterless constructors**: Most serializers require them; use the appropriate constructor attributes
3. **Test cross-format scenarios**: If data might be serialized with one format and deserialized with another, test explicitly
4. **Use TypeDecorating for polymorphism**: When storing/sending base types that may contain derived instances
5. **Consider version tolerance**: With MessagePack, add members with new `Key` values and never reuse a key
