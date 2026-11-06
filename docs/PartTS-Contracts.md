# Generated TypeScript Contracts

A TypeScript client needs three things for every .NET service it calls:

1. **The service interface**, e.g. `ITodoApi`, to type the client proxy.
2. **The DTOs** its methods take and return, with the exact wire property names.
3. **The service definition** passed to `hub.addClient`: one entry per method with its argument count,
   `callTypeId`, `wireArgCount`, and `returns` marker.

Writing these by hand duplicates the .NET contracts, and the third part is the dangerous one:
wire method names are `Service.Method:ArgCount`, so adding a parameter or a `CancellationToken`,
or renaming a method on the .NET side, breaks the client at runtime rather than at compile time.

`ActualLab.Rpc.TypeScript` generates all three from the compiled .NET contracts,
so the .NET interfaces stay the single source of truth.


## How It Works

The generator never re-derives the wire metadata. It registers your services in a real `RpcHub`
and reads the same `RpcServiceDef` / `RpcMethodDef` instances the server uses:

| Generated | Taken from |
|-----------|------------|
| Method name, `wireArgCount` | `RpcMethodDef.Name`, including `[RpcMethod(Name = ...)]` |
| `callTypeId` | `RpcMethodDef.CallType`: `1` for `[ComputeMethod]`-s, `0` for everything else |
| `returns: RpcType.noWait` / `RpcType.stream` | `Task<RpcNoWait>` / `Task<RpcStream<T>>` results |
| `remoteExecutionMode` | `RpcMethodDef.RemoteExecutionMode`, when it isn't the default |
| DTO property names and shapes | System.Text.Json metadata of `SystemJsonSerializer.DefaultOptions` |
| `T \| null` | Nullable value types and nullable reference type annotations |

Type mapping highlights:

| .NET | TypeScript |
|------|------------|
| `string`, `Guid`, `DateTime`, `TimeSpan`, `Session`, `Ulid`, `Moment`, `Symbol` | `string` |
| numeric types, including `long` and `decimal` | `number` |
| `T[]`, `List<T>`, `ImmutableArray<T>`, `ApiArray<T>` | `T[]` |
| `Dictionary<K, V>` | `Record<string, V>` |
| generic records like `Table<T>` | generic interfaces |
| enums | `enum` (or a string literal union with `JsonStringEnumConverter`) |
| `RpcStream<T>` argument / result | `RpcStream<T>` |
| `RpcStream<T>` nested into a DTO | `unknown`: convert it with `toRpcStream<T>(value, peer)` |
| `CancellationToken` | omitted; compute methods get a trailing `ctx?: AsyncContext` instead |

Value types serialized by custom converters (`Ulid`, `Moment`, ...) are mapped by serializing their default
value. Reference types the generator can't map become `unknown` with a warning:
add them to `RpcTypeScriptGeneratorOptions.KnownTypes` if you use the API directly.

::: warning
The generated DTOs describe the JSON wire format (`json5np`, the TypeScript client's default).
MessagePack formats use `[Key]`-s or property names as they're configured on the .NET side.
:::


## Usage

The `actuallab-tsgen` .NET tool takes a compiled contracts assembly and generates contracts
for every public `IRpcService` / `IComputeService` interface declared there, except `IBackendService`-s:

```powershell
dotnet tool install -g ActualLab.Rpc.TypeScript.Tool

# Generate (writes the file only if its content changes)
actuallab-tsgen MyApp.Contracts.dll --output ../web/src/contracts.generated.ts

# Only some services
actuallab-tsgen MyApp.Contracts.dll -o api.generated.ts --service ITodoApi --service IStockApi

# Verify on CI: fails if the file is missing or out of date
actuallab-tsgen MyApp.Contracts.dll -o ../web/src/contracts.generated.ts --check
```

Use the tool of the same version as the `ActualLab.*` packages your contracts reference.

### Regenerating on Build

The [TodoApp sample](https://github.com/ActualLab/Fusion/tree/master/samples/TodoApp) regenerates
its TypeScript contracts every time its `Abstractions` project is built, and only verifies them on CI
(GitHub Actions sets `CI=true`). Here is its MSBuild target, which you can adapt
by replacing the tool's `ProjectReference` with `dotnet tool run actuallab-tsgen`:

```xml
<PropertyGroup>
  <TypeScriptContractsTool>..\..\..\src\ActualLab.Rpc.TypeScript.Tool\ActualLab.Rpc.TypeScript.Tool.csproj</TypeScriptContractsTool>
  <TypeScriptContractsOutput>..\TypeScriptUI\src\contracts.generated.ts</TypeScriptContractsOutput>
  <DotNetHostPath>$(DOTNET_HOST_PATH)</DotNetHostPath>
  <DotNetHostPath Condition="'$(DotNetHostPath)' == ''">dotnet</DotNetHostPath>
</PropertyGroup>
<ItemGroup>
  <ProjectReference Include="$(TypeScriptContractsTool)" ReferenceOutputAssembly="false" />
</ItemGroup>
<Target Name="GenerateTypeScriptContracts" AfterTargets="Build"
        Condition="'$(GenerateTypeScriptContracts)' != 'false' and '$(DesignTimeBuild)' != 'true'">
  <MSBuild Projects="$(TypeScriptContractsTool)" Targets="GetTargetPath">
    <Output TaskParameter="TargetOutputs" PropertyName="_TypeScriptContractsToolPath" />
  </MSBuild>
  <PropertyGroup>
    <_TypeScriptContractsCheck Condition="'$(CI)' == 'true'"> --check</_TypeScriptContractsCheck>
  </PropertyGroup>
  <Exec Command="&quot;$(DotNetHostPath)&quot; exec &quot;$(_TypeScriptContractsToolPath)&quot; &quot;$(TargetPath)&quot; --output &quot;$(TypeScriptContractsOutput)&quot;$(_TypeScriptContractsCheck)" />
</Target>
```

### Generating From Code

`RpcTypeScriptGenerator` works with any `RpcServiceDef`-s, so you can also generate contracts
from a test or a host that already has the services registered:

```cs
var services = new ServiceCollection();
services.AddFusion().AddClient<ITodoApi>();
await using var serviceProvider = services.BuildServiceProvider();

var serviceDef = serviceProvider.RpcHub().ServiceRegistry[typeof(ITodoApi)];
var result = new RpcTypeScriptGenerator().Generate([serviceDef]);
// result.Code is the module text, result.Warnings lists everything mapped to 'unknown' or skipped
```


## Example

For these .NET contracts:

```cs
public interface ITodoApi : IComputeService
{
    [CommandHandler]
    Task<TodoItem> AddOrUpdate(Todos_AddOrUpdate command, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<TodoItem?> Get(Session session, Ulid id, CancellationToken cancellationToken = default);
}

public sealed partial record TodoItem(Ulid Id, string Title, bool IsDone = false);
public sealed partial record Todos_AddOrUpdate(Session Session, TodoItem Item) : ISessionCommand<TodoItem>;
```

The generator produces:

```ts
import type { AsyncContext } from '@actuallab/core';
import { defineRpcService } from '@actuallab/rpc';

export interface ITodoApi {
    AddOrUpdate(command: Todos_AddOrUpdate): Promise<TodoItem>;
    Get(session: string, id: string, ctx?: AsyncContext): Promise<TodoItem | null>;
}

export const TodoApiDef = defineRpcService('ITodoApi', {
    AddOrUpdate: { args: [{}] },
    Get: { args: ['', ''], callTypeId: 1 },
});

export interface TodoItem {
    id: string;
    title: string;
    isDone: boolean;
}

export interface Todos_AddOrUpdate {
    session: string;
    item: TodoItem;
}
```

And you use it exactly like a hand-written definition:

```ts
import { FusionHub } from '@actuallab/fusion-rpc';
import { TodoApiDef, type ITodoApi } from './contracts.generated.js';

const api = hub.addClient<ITodoApi>(peer, TodoApiDef);
const item = await api.Get('~', id);
```

The generator's output is also what the cross-language E2E tests (`ts/e2e/ts-dotnet-e2e.ts`)
run against a real .NET server in every serialization format.
