# Native AOT and Trimming Support

ActualLab libraries support .NET Native AOT compilation and IL trimming. This guide covers the `CodeKeeper`
infrastructure that prevents the trimmer from removing required code.


## Overview

Fusion uses runtime code generation for:
- Proxy classes (for compute services, RPC services, commanders)
- Generic `ArgumentList` implementations
- Method invocation infrastructure

When publishing with AOT or trimming, the .NET linker may remove code that appears unused at compile time
but is needed at runtime. The `CodeKeeper` infrastructure solves this by using dead-branch references
that the trimmer can see but the runtime never executes.


## RuntimeCodegen Modes

Fusion supports multiple code generation strategies:

| Mode | Description |
|------|-------------|
| `DynamicMethods` | Runtime IL generation (default for JIT) |
| `InterpretedExpressions` | Expression tree interpretation (AOT-compatible) |
| `CompiledExpressions` | Compiled expression trees |

The mode is selected based on the runtime environment:

<!-- snippet: PartAOT_CheckMode -->
```cs
// Check the current mode
Console.WriteLine($"RuntimeCodegen.Mode: {RuntimeCodegen.Mode}");

// In Native AOT:
// - NativeMode will be InterpretedExpressions
// - DynamicMethods is not available
```
<!-- endSnippet -->


## CodeKeeper Infrastructure

### Base CodeKeeper Class

`CodeKeeper` is a static utility that prevents trimming of required code. It uses the dual-mechanism
approach: `[DynamicallyAccessedMembers(All)]` preserves metadata, while `typeof(T).GetMembers()` in
a dead branch forces ILC to generate native code (critical for struct generics).

<!-- snippet: PartAOT_CodeKeeperBase -->
```cs
public static class CodeKeeper
{
    // Never evaluates to true at runtime, but compiler can't prove it
    public static readonly bool AlwaysFalse;
    public static readonly bool AlwaysTrue;

    // Extension point for downstream projects
    public static IExtension? Extension { get; set; }

    // Register a type to prevent trimming
    public static void Keep<T>();
    public static void Keep(Type type);
    public static void Keep(string assemblyQualifiedTypeName);

    // Register serializable types
    public static void KeepSerializable<T>();

    public interface IExtension { ... }
}
```
<!-- endSnippet -->

`KeepSerializable<T>()` preserves the MessagePack serialization code for `T`, gated by a feature
switch (see [Feature Switches](#feature-switches)). Serializer packages add their own keep-code
through a `CodeKeeper.IExtension`: `ActualLab.Serialization.MemoryPack` installs
`MemoryPackCodeKeeperExtension`, so MemoryPack code is preserved only in apps that reference that
package.

### Extension Architecture

All keepers are static classes with nested `IExtension` interfaces and `Extension` properties.
The old virtual dispatch inheritance chain is replaced by composition in extension implementations.

Each subsystem provides a `ProxyCodeKeeper.IExtension` implementation:

| Class | Purpose |
|-------|---------|
| `ProxyCodeKeeper` | Base proxy and ArgumentList support |
| `RpcProxyCodeKeeperExtension` | RPC serialization, calls, and service infrastructure |
| `CommanderProxyCodeKeeperExtension` | Commander command handlers and contexts |
| `FusionProxyCodeKeeperExtension` | Compute methods and Fusion-specific types |

Extensions compose by triggering each other's static constructors. `FusionProxyCodeKeeperExtension`
creates `CommanderProxyCodeKeeperExtension`, which creates `RpcProxyCodeKeeperExtension`, which
creates `RpcMethodDefCodeKeeperExtension`. Module initializers in each assembly assign the extension
to the corresponding static property at runtime.


## Usage in Native AOT Applications

No manual setup is needed. Module initializers automatically configure the extensions:

<!-- snippet: PartAOT_CompleteExample -->
```cs
#pragma warning disable IL3050

public static async Task Main()
{
    // No CodeKeeper setup needed — module initializers handle it automatically.
    // FusionModuleInitializer sets ProxyCodeKeeper.Extension = new FusionProxyCodeKeeperExtension()
    // RpcModuleInitializer sets MethodDefCodeKeeper.Extension = new RpcMethodDefCodeKeeperExtension()

    // Configure services as usual
    var services = new ServiceCollection()
        .AddLogging(l => l.AddSimpleConsole())
        .AddFusion(fusion => {
            fusion.Rpc.AddWebSocketClient();
            fusion.AddServerAndClient<ITestService, TestService>();
        })
        .AddSingleton(_ => RpcOutboundCallOptions.Default with {
            RouterFactory = methodDef => args => RpcRef.Loopback,
        })
        .BuildServiceProvider();

    // Use services
    var client = services.RpcHub().GetClient<ITestService>();
    var now = await client.GetTime();
}

#pragma warning restore IL3050
```
<!-- endSnippet -->


## Keeping Custom Types

### For Service Methods

Implement `ProxyCodeKeeper.IExtension` to keep application-specific types:

<!-- snippet: PartAOT_CustomCodeKeeperMethods -->
```cs
public class MyAppCodeKeeperExt : ProxyCodeKeeper.IExtension
{
    public void KeepProxy<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TBase,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProxy>()
        where TBase : IRequiresAsyncProxy
        where TProxy : IProxy
    { }

    public void KeepMethodArgument<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TArg>(
        string name, int index)
    { }

    public void KeepMethodResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        string name)
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        // Keep types used as method results
        CodeKeeper.Keep<MyResult>();

        // Keep serializable types
        CodeKeeper.KeepSerializable<MyDto>();
    }
}
```
<!-- endSnippet -->

### For Proxy Types

If you pre-generate proxy classes, keep them explicitly:

<!-- snippet: PartAOT_CustomCodeKeeperProxy -->
```cs
public class MyAppProxyCodeKeeperExt : ProxyCodeKeeper.IExtension
{
    public void KeepProxy<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TBase,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProxy>()
        where TBase : IRequiresAsyncProxy
        where TProxy : IProxy
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        // Keep service interface and its generated proxy
        CodeKeeper.Keep<IMyService>();
        CodeKeeper.Keep<MyServiceProxy>();
    }

    public void KeepMethodArgument<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TArg>(
        string name, int index)
    { }

    public void KeepMethodResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        string name)
    { }
}

// Use your custom code keeper extension
// ProxyCodeKeeper.Extension = new MyAppProxyCodeKeeperExt();
```
<!-- endSnippet -->


## How CodeKeeper Works

CodeKeepers use a dead-branch pattern to prevent trimming while avoiding runtime overhead:

<!-- snippet: PartAOT_HowCodeKeeperWorks -->
```cs
public static class MyCodeKeeper
{
    public static void KeepMyType()
    {
        // This condition is always true at runtime, but the compiler can't prove it
        if (CodeKeeper.AlwaysTrue)
            return;

        // This code is never executed, but the trimmer sees the reference
        // and preserves the type
        CodeKeeper.Keep<MyType>();
    }
}
```
<!-- endSnippet -->

The `AlwaysFalse`/`AlwaysTrue` constants use runtime values that the compiler cannot evaluate:

<!-- snippet: PartAOT_AlwaysFalseImplementation -->
```cs
public static readonly bool AlwaysFalse = RandomShared.NextDouble() > 2;
```
<!-- endSnippet -->

Since `AlwaysFalse` is evaluated at runtime, ILC must assume either branch is reachable and generate
code for both.


## Source Generator

The `ActualLab.Generators` proxy source generator emits `KeepCode()` methods as `[ModuleInitializer]`s
in each generated proxy. These call `ProxyCodeKeeper.KeepProxy<TBase, TProxy>()`,
`ProxyCodeKeeper.KeepAsyncMethod<TResult, T0, ...>()`, etc. inside `if (CodeKeeper.AlwaysFalse) { ... }`
blocks. This ensures ILC traces through the entire extension chain and preserves all required types.

**Important**: The source generator requires types to be in a namespace. Types declared at the top level
(without a namespace) will be silently skipped.

To skip proxy generation for an assembly, e.g. one that references `ActualLab.Generators` only for
other reasons, define `ACTUALLAB_NO_PROXIES`. It saves the build time the generator spends
inspecting its types; such an assembly gets no generated proxies.

### Serialization Roots

Serializers create the formatter of a closed generic type like `Option<int>`, `List<Moment>` or
`PageRef<long>` with `MakeGenericType`. NativeAOT can do that only for instantiations it compiled,
and it compiles none of them for value-type arguments on its own. So `ActualLab.Generators` can
also emit one `[ModuleInitializer]` per assembly, `ActualLabSerializationRoots`, that keeps the
closed formatters and converters of every closed generic type the assembly uses:

- in the signatures of its non-private methods, which include RPC service methods;
- in the data members of its serializable types (`[DataContract]` or `[MessagePackObject]`),
  and of the serializable types those reference;
- in the public members of the types its `JsonSerializerContext`s list.

For MessagePack, these are the formatters named by `[MessagePackFormatter]`, the ones MessagePack's
source generator creates for generic `[MessagePackObject]` types, and MessagePack's own array,
`List<T>`, `Dictionary<TKey, TValue>` and `Nullable<T>` formatters. An assembly-level attribute
suppresses IL3050 on the resolver MessagePack's source generator creates (under its default name),
since its `MakeGenericType` calls are what these roots cover. For System.Text.Json, these are the
converters the `[JsonConverter]` factories of generic types create.

What isn't covered:

- a closed generic type that is serialized but appears in none of the places above, e.g. one
  passed to a serializer directly: use it in a signature or data member, or keep its formatter
  with `CodeKeeper.Keep<TFormatter>()`;
- a nested generic `[MessagePackObject]` type: MessagePack names its formatter differently, so
  warning `ALG0005` asks you to move it out or keep its formatter yourself;
- types the generated code can't name: `dynamic`, pointers, file-local and obsolete types, and
  the types of other source generators.

The roots are opt-in: only an app needs them, and only in the assemblies that declare its contracts
and serializable types. Define `ACTUALLAB_SERIALIZATION_ROOTS` there; ActualLab's own libraries do.
`ACTUALLAB_NO_PROXIES`, on the other hand, turns proxy generation off for an assembly. Both go to
`DefineConstants`:

```xml
<PropertyGroup>
  <DefineConstants>$(DefineConstants);ACTUALLAB_SERIALIZATION_ROOTS</DefineConstants>
</PropertyGroup>
```

The `NativeAotFull` sample shows the same roots written by hand, one file per serializer in its
[`Roots`](https://github.com/ActualLab/Fusion/tree/master/samples/NativeAotFull/Roots) folder:
`MessagePackRoots.cs` and `SystemJsonRoots.cs` mirror what the generator emits, while
`MemoryPackRoots.cs`, `NerdbankRoots.cs` and `NewtonsoftJsonRoots.cs` show what these serializers
need, since the generator doesn't cover them.

### System.Text.Json

Apps that disable reflection-based System.Text.Json, which NativeAOT and trimmed apps do by default,
need source-generated metadata for every type the `json5` RPC formats, and `SystemJsonSerializer`
in general, serialize. On .NET 8+, `SystemJsonSerializer.TypeInfoResolver` collects it: ActualLab's
libraries add their contexts there, which cover primitives and their own types, and an app adds its
own one at startup:

```cs
[JsonSerializable(typeof(Option<int>))]
[JsonSerializable(typeof(OrderDto))]
public sealed partial class AppJsonContext : JsonSerializerContext;

SystemJsonSerializer.TypeInfoResolver.Add(AppJsonContext.Default);
```

Add it before the types it covers are first serialized: System.Text.Json caches each lookup,
including a failed one. `SystemJsonSerializer`'s default options try these contexts first and fall
back to reflection only where it's enabled. If you replace `SystemJsonSerializer.DefaultOptions`,
set `TypeInfoResolver = SystemJsonSerializer.CreateDefaultTypeInfoResolver()` to keep them.

In an assembly that defines `ACTUALLAB_SERIALIZATION_ROOTS` and declares a `JsonSerializerContext`,
`ActualLab.Generators` checks the RPC service methods it declares: warning `ALG0004` names each type
they take or return that no context covers, either this assembly's own or one of a referenced
assembly that defines `ACTUALLAB_SERIALIZATION_ROOTS` too. A type counts as covered when a context
lists it or it's reachable from a listed type through public properties, fields, element types or
type arguments; the type arguments of a type with its own `[JsonConverter]` aren't, since its
converter serializes them. The check doesn't verify that the context is added to
`SystemJsonSerializer.TypeInfoResolver`.

## Project Configuration

### Enable AOT Publishing

In your `.csproj`:

```xml
<PropertyGroup>
    <PublishAot>true</PublishAot>
</PropertyGroup>
```

### Suppress Trimming Warnings

For code that uses reflection intentionally:

<!-- snippet: PartAOT_SuppressWarnings -->
```cs
[UnconditionalSuppressMessage("Trimming", "IL2026",
    Justification = "Types are preserved by CodeKeeper")]
[UnconditionalSuppressMessage("Trimming", "IL3050",
    Justification = "Types are preserved by CodeKeeper")]
public static void MyMethod() { }
```
<!-- endSnippet -->

### DynamicallyAccessedMembers

For generic parameters that need reflection:

<!-- snippet: PartAOT_DynamicallyAccessedMembers -->
```cs
public static void ProcessType<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
{
    // T's members are preserved by the trimmer
}
```
<!-- endSnippet -->


## Feature Switches

ActualLab libraries expose [feature switches](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options#trimming-framework-library-features)
that let you trim away optional code paths. All of them are **on by default**; disable the ones your
app never uses to reduce the published binary size.

| Switch | Default | Effect when disabled |
|--------|---------|----------------------|
| `ArgumentList.AllowGenerics` | `true` | `ArgumentList` stops using generic `ArgumentListG*<...>` implementations, so they can be trimmed away |
| `MessagePackByteSerializer.IsEnabled` | `true` | `CodeKeeper.KeepSerializable<T>()` stops preserving MessagePack serialization code |

The serializer switch matters for RPC: `KeepSerializable<T>()` preserves MessagePack serialization
code for every registered type, since the default RPC formats (`msgpack*`) use it. If your app uses
only other formats (e.g. only `mempack*` from `ActualLab.Serialization.MemoryPack`), disable it to
drop that keep-code. The `MemoryPackByteSerializer.IsEnabled` switch was removed in v16: MemoryPack
keep-code now exists only if you reference `ActualLab.Serialization.MemoryPack`.

To disable a switch, add a trimmed `RuntimeHostConfigurationOption` to your `.csproj`:

```xml
<ItemGroup>
    <RuntimeHostConfigurationOption
        Include="MessagePackByteSerializer.IsEnabled"
        Value="false"
        Trim="true" />
</ItemGroup>
```

`Trim="true"` makes the value a compile-time constant for the trimmer, so the disabled branches are
removed from the output. You can also flip a switch at runtime (without the trimming benefit) via
`AppContext.SetSwitch(...)` before any ActualLab code runs.


## ArgumentList Support

`ArgumentList` requires special handling because it uses generic types based on argument count:

<!-- snippet: PartAOT_ArgumentListSupport -->
```cs
// These types are auto-generated and need preservation
var l0 = ArgumentList.New();                    // ArgumentList0
var l2 = ArgumentList.New(1, "s");              // ArgumentList2<int, string>
var l10 = ArgumentList.New(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);  // ArgumentList10<...>
```
<!-- endSnippet -->

The `ArgumentListCodeKeeper` (invoked through `ProxyCodeKeeper`) preserves these types:

```cs
public void KeepArgumentListArgument<T>()
{
    // Keeps ArgumentListN types that include T as a type argument
}
```


## Limitations

1. **Dynamic proxy generation**: Native AOT doesn't support `System.Reflection.Emit` at runtime.
   Proxies must be either:
   - Pre-generated at build time (source generators)
   - Using interpreted expressions mode

2. **Expression tree limitations**: Some complex expressions may not work in interpreted mode.
   Test your application in AOT mode during development.

3. **Serialization**: All serialized types must be annotated with appropriate attributes
   (`[DynamicallyAccessedMembers]`, `[MessagePackObject]`, `[MemoryPackable]` if you use MemoryPack, etc.)

4. **Namespace requirement**: Service types and their implementations must be declared inside
   a namespace for the proxy source generator to produce output.


## Best Practices

1. **Use the source generator**: Reference `ActualLab.Generators` as an analyzer in your project.
   It automatically generates proxy types and `KeepCode()` module initializers.

2. **Use FusionProxyCodeKeeperExtension**: It includes all subsystems (Commander, RPC, Fusion).
   Only create a custom extension if you need to add application-specific types.

3. **Test in AOT mode**: Run `dotnet publish -c Release` and test the AOT binary regularly
   during development to catch trimming issues early. The `NativeAotFull` sample does this for
   Fusion itself: it round-trips the types that are sensitive to trimming over every wire format.

4. **Annotate serializable types**: Use `[DynamicallyAccessedMembers]` or serialization attributes
   on all types that may be serialized/deserialized.

5. **Check RuntimeCodegen.Mode**: If your code behaves differently based on the codegen mode,
   log it at startup for easier debugging.


## Related Topics

- [NativeAotQuirks](https://github.com/ActualLab/NativeAotQuirks) - Test project documenting NativeAOT
  code generation, trimming, and type retention quirks. The `CodeKeeper` approach is based on findings
  from this project.
- [Serialization](./PartS.md) - Type annotations for serialization
- [Interceptors and Proxies](./PartAP.md) - Proxy generation details
