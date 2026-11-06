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

Reference the `ActualLab.Rpc.TypeScript.Generator` package from your contracts project
and tell it where to write the file:

```xml
<PropertyGroup>
  <RpcTypeScriptOutput>..\Web\src\contracts.generated.ts</RpcTypeScriptOutput>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="ActualLab.Rpc.TypeScript.Generator" PrivateAssets="all" />
</ItemGroup>
```

Every build of the project then regenerates the file from its assembly, and writes it only if its content changes.
The generator covers every public `IRpcService` / `IComputeService` interface declared there,
except `IBackendService`-s. The package is a development dependency: it adds no runtime references.

| MSBuild property | Meaning |
|------------------|---------|
| `RpcTypeScriptOutput` | The file to generate, relative to the project. Generation is off when it's empty. |
| `RpcTypeScriptServices` | Comma-separated service type name patterns, where `*` matches any sequence of characters, e.g. `ITodo*,IStockApi`. Each pattern is matched against both the short and the full type name. Default: `*`. |
| `RpcTypeScriptCheck` | `true` fails the build if the file is missing or out of date instead of updating it. Default: `true` when `CI` is `true` (GitHub Actions and most other CI systems set it). |
| `RpcTypeScriptTargetFramework` | The target framework to generate from when the project has many. Default: the first one. |

Use the generator of the same version as the `ActualLab.*` packages your contracts reference:
it loads the contracts assembly together with its own `ActualLab.*` assemblies.

You can also run the generator on any compiled assembly by hand. In the Fusion repository, that's:

```powershell
dotnet run --project src/ActualLab.Rpc.TypeScript.Generator -- `
    MyApp.Contracts.dll --output contracts.generated.ts --services "ITodo*,IStockApi"
```

Add `--check` to verify the file instead of writing it.

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
