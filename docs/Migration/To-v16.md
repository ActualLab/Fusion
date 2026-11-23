# Migrating from v15 to v16

v16 moves MemoryPack and Newtonsoft.Json support out of `ActualLab.Core` into separate packages,
makes MessagePack the default binary serializer and RPC format, and replaces the process-wide RPC
format registry with per-container wire format configuration.

The wire formats themselves don't change: `mempack*`, `njson*` and the persisted Newtonsoft.Json
data are byte-for-byte the same as in v15, except for type-decorated payloads naming a type that
moved to another assembly (see [Upgrade Constraints](#upgrade-constraints)). What changes is the
defaults and how you opt in. Most
apps only need [Step 1](#step-1-reference-the-packages-you-use) and
[Step 4](#step-4-configure-rpc-formats-per-container).

## What's New in v16

### Serialization

- **Lighter `ActualLab.Core`.** System.Text.Json and MessagePack are built in; MemoryPack,
  Newtonsoft.Json and Nerdbank.MessagePack support come from separate packages
  (`ActualLab.Serialization.MemoryPack`, `.NewtonsoftJson`, `.NerdbankMessagePack`). Core also no
  longer depends on `Ulid`, `System.Linq.Async`, `CommunityToolkit.HighPerformance`,
  `K4os.Compression.LZ4`, `Microsoft.Extensions.Hosting` (only its abstractions) and
  `Microsoft.Extensions.Options.DataAnnotations` &ndash; see [Step 8](#step-8-reference-the-hosting-packages-you-use).
- **MessagePack is the default** binary serializer, and `msgpack6` is the default RPC format
  (was `mempack6`).
- **Nerdbank.MessagePack support is experimental**, and **MemoryPack is on its way out**: it stays
  for compatibility with v15 peers, but gets no new features (e.g. no NativeAOT source generation
  support). Prefer MessagePack or System.Text.Json for new contracts.
- **Newtonsoft.Json without attributes.** ActualLab types carry only System.Text.Json and
  `[DataContract]` attributes; `NewtonsoftJsonContractResolver` maps them for Newtonsoft.Json
  (see [Step 5](#step-5-custom-newtonsoft-json-settings-need-newtonsoftjsoncontractresolver)).
- **`UniSerialized<T>` and the type-decorating wrappers** are plain wrappers serialized by whatever
  serializer is running, and `IByteSerializer` / `ITextSerializer` got scoped `Read` / `Write`
  (see [Other Breaking Changes](#other-breaking-changes)).

### RPC

- **Per-container wire formats.** Each DI container configures its own format set, default format
  and backend-only formats with `RpcBuilder.ConfigureWireFormats(...)`; the process-wide registry
  is gone. The default set is slim, and everything else is opt-in
  (see [Step 4](#step-4-configure-rpc-formats-per-container)).
- **Built-in LZ4 with preset dictionaries.** LZ4 now lives in `ActualLab.Core` itself (a vendored
  copy of K4os.Compression.LZ4). `ByteCompression` is the codec abstraction: it creates
  compressors, decompressors and preset dictionaries, which are prepared once and shared by all
  compressors, so even the first frames after a compression context reset compress well.
  `f.AddLZ4Variants(suffix, dictionary, mode)` adds the LZ4 variants of the binary formats
  (see [LZ4 Dictionaries](../PartR-Serialization.md#lz4-dictionaries) and
  [Step 9](#step-9-lz4-variants-take-a-suffix)).

### NativeAOT and Trimming

- **Every serializer works under NativeAOT.** The `NativeAot` sample shows the built-in formats,
  and `NativeAotFull` every serializer package, with one file per serializer listing what it needs.
- **Opt-in serialization roots.** Define `ACTUALLAB_SERIALIZATION_ROOTS` in an assembly, and
  `ActualLab.Generators` roots the closed generic MessagePack formatters and System.Text.Json
  converters its contracts need, and checks its RPC contracts against its `JsonSerializerContext`s
  (warnings `ALG0004`, `ALG0005`; `[assembly: IgnoreJsonCoverage(...)]` excludes a type).
  `ACTUALLAB_NO_PROXIES` turns proxy generation off for an assembly that doesn't need it.
- **Source-generated System.Text.Json metadata.** Add your `JsonSerializerContext` to
  `SystemJsonSerializer.TypeInfoResolver` before its types are first serialized.
- **No false-positive warnings.** Registering Fusion services, compute methods, RPC clients or
  serializer packages no longer produces trimming / AOT warnings from ActualLab code.

See [Native AOT and Trimming](../PartAOT.md) for the details.

### Other Changes

- `Type.GetDefaultValue()` returns `null` for `Nullable<T>` (it returned the default of `T`), which
  also fixes a `null` `int?` RPC argument arriving as `0` over the JSON formats.
- `LZ4ByteDecompressor` reports malformed input with `FormatException`.
- .NET Framework fixes: `TypeRef` resolution of GAC and culture-specific assemblies, `TickSource`
  waiters, `EncoderExt` / `DecoderExt` on empty input, and the NetFx RPC WebSocket server rejecting
  unsupported formats with close code 4001.
- `RpcStream` no longer resends an item it can't serialize or deserialize forever: the stream fails.

## Upgrade Constraints

- **Hosts that share the operation log or backend RPC upgrade together.** `IAuthBackend`,
  `IKeyValueStore` and their commands moved to another assembly, and the assembly name is part of
  the type name these commands are serialized with.
- **v15 clients default to `mempack6`.** To keep them working while you roll out v16, the server
  must serve it: add `f.AddMemoryPack()` there (see [Step 4](#step-4-configure-rpc-formats-per-container)).
- **Moved type names only resolve forward.** v16 reads payloads that name a moved type by its old
  assembly (`TypeRef.MovedTypes`), but it writes the new name, which v15 can't resolve. This
  affects type-decorated payloads (e.g. `PropertyBag` values) that contain `ApiOption`,
  `ApiNullable`, `ApiNullable8` or the moved backend commands.

## Step 1: Reference the packages you use

| If you use | Reference |
|------------|-----------|
| `MemoryPackByteSerializer`, `MemoryPackSerialized<T>`, `[MemoryPackable]` types with ActualLab members, `mempack*` RPC formats | `ActualLab.Serialization.MemoryPack` |
| `ApiOption<T>`, `ApiNullable<T>`, `ApiNullable8<T>` | `ActualLab.Serialization.MemoryPack` |
| `NewtonsoftJsonSerializer`, `NewtonsoftJsonSerialized<T>`, `njson*` RPC formats | `ActualLab.Serialization.NewtonsoftJson` |
| `nmsgpack*` RPC formats | `ActualLab.Serialization.NerdbankMessagePack` |
| `OptionSet`, `ImmutableOptionSet`, `FuncByteSerializer`, `FuncTextSerializer` (obsolete) | `ActualLab.Core.Legacy` |
| `IAuthBackend`, `IKeyValueStore` and their commands without `ActualLab.Fusion.Ext.Services` | `ActualLab.Fusion.Ext.Backend.Contracts` |
| `FileSystemCache` | `ActualLab.Plugins` |

`ActualLab.Serialization.MemoryPack` targets .NET Core 3.1+ and .NET Standard 2.1; it has no
.NET Framework or .NET Standard 2.0 build.

The packages register their formatters, converters and `SerializerKind` entries from a module
initializer when their assembly loads (on .NET 5+). If nothing in your app touches the package
before the first serialization (e.g. you only use `[MemoryPackable]` types), or you target
.NET Core 3.1 / .NET Standard 2.1, call `MemoryPackSerialization.Register()` /
`NewtonsoftJsonSerialization.Register()` at startup.

`ActualLab.Fusion.Ext.Services` type-forwards `IAuthBackend`, `IKeyValueStore` and their commands,
so code that uses them compiles unchanged.

## Step 2: Fix MEMPACK019 in your `[MemoryPackable]` types

ActualLab types no longer carry `[MemoryPackable]`; the package serializes them with hand-written
formatters registered at runtime. The MemoryPack source generator can't see those formatters, so
a `[MemoryPackable]` type with a member like `Session`, `Symbol`, `TypeRef`, `PropertyBag`,
`Option<string>`, `Result<T>` or `User` fails to compile with **MEMPACK019**. Add
`[MemoryPackAllowSerialize]` to each such member:

```cs
[DataContract, MessagePackObject, MemoryPackable(GenerateType.VersionTolerant)]
[method: System.Text.Json.Serialization.JsonConstructor, SerializationConstructor, MemoryPackConstructor]
public sealed partial record Note(
    // ActualLab types: [MemoryPackAllowSerialize] is required since v16
    [property: DataMember(Order = 0), Key(0), MemoryPackOrder(0), MemoryPackAllowSerialize] Session Session,
    [property: DataMember(Order = 1), Key(1), MemoryPackOrder(1), MemoryPackAllowSerialize] Symbol Tag,
    [property: DataMember(Order = 2), Key(2), MemoryPackOrder(2), MemoryPackAllowSerialize] Option<string> Title,
    // Not needed for ApiArray/ApiList/ApiSet/ApiMap, unmanaged structs
    // and ApiOption/ApiNullable over an unmanaged T
    [property: DataMember(Order = 3), Key(3), MemoryPackOrder(3)] ApiArray<string> Lines,
    [property: DataMember(Order = 4), Key(4), MemoryPackOrder(4)] ApiOption<int> Priority);
```

You don't need it for `ApiArray<T>`, `ApiList<T>`, `ApiSet<T>`, `ApiMap<TKey, TValue>`, unmanaged
structs (e.g. `Moment`, `CpuTimestamp`), and `ApiOption<T>` / `ApiNullable<T>` over an
unmanaged `T`. Rebuild every assembly that declares such types: code the generator produced for
v15 calls MemoryPack members ActualLab types no longer have.

If a `[MemoryPackable]` type of yours nests a generic ActualLab type inside another generic
(e.g. a `Dictionary<string, Option<MyStruct>>` member), call `MemoryPackSerialization.Register<T>()`
for that type at startup, so the nested type is serialized the same way as in v15.

## Step 3: Drop MemoryPack and Newtonsoft.Json attributes you don't need

Your API records no longer need MemoryPack or Newtonsoft.Json attributes unless you use those
serializers: `[DataContract]`, MessagePack's `[MessagePackObject]` / `[Key]` and the System.Text.Json
attributes are the baseline (see [Annotating Types](../PartS.md#annotating-types-for-serialization)).

## Step 4: Configure RPC formats per container

The default RPC format is now `msgpack6` (was `mempack6`), and the default format set is slim:
`json5`, `json5np`, `msgpack5`, `msgpack5c`, `msgpack6`, `msgpack6c`. Everything else &ndash;
including the `-lz4` variants &ndash; is opt-in via `RpcBuilder.ConfigureWireFormats`:

```cs
services.AddRpc().ConfigureWireFormats(f => {
    f.AddMemoryPack();     // ActualLab.Serialization.MemoryPack: mempack5(c), mempack6(c)
    f.AddNewtonsoftJson(); // ActualLab.Serialization.NewtonsoftJson: njson5, njson5np
    f.AddLZ4Variants("lz4"); // "-lz4" variants of the formats added so far
    f.DefaultFormat = f["msgpack6c-lz4"];
});
```

- A client that pins a format (e.g. `f.DefaultFormat = f["msgpack6c-lz4"]`) needs a server that serves it,
  so add the same formats on the server.
- `RpcNerdbankSerializationFormat.Register()` is gone; use `f.AddNerdbankMessagePack()`.
- RPC formats are now "wire formats": `RpcSerializationFormat` became `RpcWireFormat`,
  `RpcSerializationFormatResolver` became `RpcWireFormatResolver`, and `RpcHub.SerializationFormats`,
  `RpcPeer.SerializationFormat` and `RpcRef.SerializationFormat` became `WireFormats` / `WireFormat`.
- The built-in formats (`SystemJsonV5`, `MessagePackV6`, ...) moved from `RpcSerializationFormat` to
  `RpcDefaultWireFormats`; the package ones are in `RpcMemoryPackWireFormats`,
  `RpcNewtonsoftJsonWireFormats` and `RpcNerdbankWireFormats`.
- `RpcSerializationFormat.All`, `RpcSerializationFormat.Default`, `RpcSerializationFormatResolver.DefaultFormats`
  and the `*_LZ4` / `*_LZ4F` format statics are gone. Configure each container with
  `rpc.ConfigureWireFormats(f => ...)` instead: `f.Add(...)` / `f.Remove(...)`, `f.AddLZ4Variants("lz4")`,
  `f.DefaultFormat` (instead of assigning `RpcSerializationFormatResolver.Default`) and
  `f.BackendOnlyFormatKeys` (instead of `RpcSerializationFormatResolver.DefaultClientDeniedFormatKeys`;
  the process-wide defaults are the read-only `RpcDefaultWireFormats.DefaultFormat` and `.BackendOnlyFormatKeys`).

See [RPC Wire Formats](../PartR-Serialization.md#configuring-formats) for the builder API.

## Step 5: Custom Newtonsoft.Json settings need `NewtonsoftJsonContractResolver`

ActualLab types no longer carry Newtonsoft.Json attributes or `[JsonConverter]`s. The default
`NewtonsoftJsonSerializer.DefaultSettings` use `NewtonsoftJsonContractResolver`, which reads their
System.Text.Json and `[DataContract]` attributes and supplies their converters. If you create
`JsonSerializerSettings` yourself, set `ContractResolver = new NewtonsoftJsonContractResolver()` (or a
subclass of it) &ndash; with the stock `DefaultContractResolver`, types like `Symbol`, `Moment` or
`Session` serialize differently.

Persisted data written by Fusion (the EF operation log, the key-value store and auth data of
`ActualLab.Fusion.Ext.Services`) still uses Newtonsoft.Json, in the same format as before; these
packages reference `ActualLab.Serialization.NewtonsoftJson` themselves.

## Step 6: `Ulid` needs its own MessagePack resolver

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

## Step 7: `ApiOption`, `ApiNullable` and `ApiNullable8` moved to `ActualLab.Serialization.MemoryPack`

These types exist only for MemoryPack contracts &ndash; see
[MemoryPack and Unmanaged Structs](../PartS.md#memorypack-and-unmanaged-structs) for why.

- Reference `ActualLab.Serialization.MemoryPack` wherever you use them; namespaces are unchanged.
  The package has no .NET Framework / .NET Standard 2.0 build, so these types aren't available
  there anymore: use `Option<T>` / `T?` in contracts shared with such targets.
- `Option<T>.ToApiOption()` is now an extension method in that package, and `Option<T>` no longer
  implements `IConvertibleTo<ApiOption<T>>`. `ApiOption<T>.ToOption()` is unchanged.
- `ActualLab.Serialization.NerdbankMessagePack` no longer has converters for them, so don't use
  them with the `nmsgpack*` formats. Before v16, `ApiOption<T>` was written as `[]` / `[v]`, and
  `ApiNullable<T>` / `ApiNullable8<T>` as `nil` / the plain value.
- Payloads that name them with their old assembly (`ActualLab.Core`) still resolve via
  `TypeRef.MovedTypes`.

## Step 8: Reference the hosting packages you use

`ActualLab.Core` references `Microsoft.Extensions.Hosting.Abstractions` instead of
`Microsoft.Extensions.Hosting`, and no longer references `Microsoft.Extensions.Options.DataAnnotations`.
ASP.NET Core apps aren't affected: the shared framework includes all of them. Other apps (console,
worker, desktop, test projects) that used these packages through Core now need their own references:

| If you use | Reference |
|------------|-----------|
| `Host.CreateDefaultBuilder`, `HostBuilder`, `Host.CreateApplicationBuilder` | `Microsoft.Extensions.Hosting` |
| `AddConsole()`, `AddDebug()` and other logging providers | `Microsoft.Extensions.Logging.Console`, `.Debug`, ... |
| JSON / environment variable / command line configuration providers | `Microsoft.Extensions.Configuration.Json`, ... |
| `ValidateDataAnnotations()` on options | `Microsoft.Extensions.Options.DataAnnotations` |

`IHostedService`, `BackgroundService`, `IHostEnvironment`, `ILogger`, `IOptions<T>` and configuration
binding keep working without them.

## Step 9: LZ4 variants take a suffix

If you configured formats with 16.0.4 or earlier v16 builds: the suffix of the LZ4 variants is now
required and used as is, and adding a variant whose key already exists fails instead of keeping
the existing format.

| Before | Now |
|--------|-----|
| `f.AddLZ4Variants()` | `f.AddLZ4Variants("lz4")` |
| `f.AddLZ4Variants(RpcCompressionMode.Full)` | `f.AddLZ4Variants("lz4f", compressionMode: RpcCompressionMode.Full)` |
| `f.AddLZ4Variants(filter, modes)` | `f.AddLZ4Variants(suffix, compressionMode: mode, filter: filter)`, once per mode |
| `format.WithLZ4()` / `format.WithLZ4(RpcCompressionMode.Full)` | `format.WithLZ4("lz4")` / `format.WithLZ4("lz4f", compressionMode: RpcCompressionMode.Full)` |

The keys stay the same (`-lz4`, `-lz4f`), so peers on either version still agree. Variants with a
preset dictionary need a suffix of their own, e.g. `f.AddLZ4Variants("lz4d1", dictionary)`.

## Other Breaking Changes

- **Serializer API.** `IByteSerializer` / `ITextSerializer` expose `ReadUnscoped` / `WriteUnscoped`;
  `Read` / `Write` became extension methods that open a `ByteSerializer.Scope`. The asymmetric and
  converting byte/text serializers are gone.
- **`UniSerialized<T>` and `TypeDecoratingUniSerialized<TSchema, T>`** are plain `Value` wrappers:
  each format's converter serializes `Value` with the serializer that is currently running
  (`ByteSerializer.Current`). Wire shapes are unchanged, except Nerdbank's `UniSerialized<T>`, which
  goes from `{"MessagePack": bin}` to `[bin]`.
- **`TSchema : TypeSchema` and `TSanitizer : Sanitizer` type parameters lost their `new()`
  constraint** (`PropertyBag<TSchema>`, `SanitizedString<TSanitizer>`, the type-decorating
  wrappers). A custom `TypeSchema` or `Sanitizer` still needs a public parameterless constructor,
  but the compiler no longer checks it, and in trimmed apps the trimmer no longer keeps it: root it
  with a static constructor, e.g. `static MySchema() => CodeKeeper.Keep<MySchema>();` (see
  [PropertyBag Serialization](../PartS.md#propertybag-serialization)). Without it, a trimmed app
  fails with `MissingMethodException` the first time the type is used.
- **Moved type names.** `TypeRef.MovedTypes` maps the names of types that moved to another
  assembly, so payloads written by v15 still resolve them.
- **`ActivatorExt.New<T>()`** is gone; use `Activator.CreateInstance<T>()`.
- **Native AOT:** the `MemoryPackByteSerializer.IsEnabled` trimming switch is gone &ndash; MemoryPack
  keep-code now exists only if you reference `ActualLab.Serialization.MemoryPack`.
- **Compression:** `ByteCompressor.Reset()` and `ByteDecompressor.Reset()` are no longer abstract;
  a custom codec overrides `ResetCore(object? dictionary)` instead. `RpcCompressionFormat.LZ4` is
  unchanged, and `RpcCompressionFormat.New(...)` / `NewLZ4(...)` create formats with a dictionary.

## Further Reading

- [Unified Serialization](../PartS.md) &ndash; serializers, wrappers and the optional packages
- [Native AOT and Trimming](../PartAOT.md) &ndash; serialization roots, source-generated JSON metadata
- [RPC Wire Formats](../PartR-Serialization.md) &ndash; the wire formats and their configuration
- [NuGet Packages](../NuGet-Packages.md) &ndash; what each package contains
- [Changelog](../CHANGELOG.md)
