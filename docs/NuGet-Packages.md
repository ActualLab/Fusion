# NuGet Packages

All Fusion packages are
[available on NuGet](https://www.nuget.org/packages?q=tags%3A%22actual_lab_fusion%22+Owner%3A%22Actual.chat%22).  
[![Build](https://github.com/ActualLab/Fusion/workflows/Build/badge.svg)](https://github.com/ActualLab/Fusion/actions?query=workflow%3A%22Build%22) [![NuGetVersion](https://img.shields.io/nuget/v/ActualLab.Core)](https://www.nuget.org/packages?q=tags%3A%22actual_lab_fusion%22+Owner%3A%22Actual.chat%22)

You should reference these packages based on your project type:

- `ActualLab.Fusion.Server` &ndash; for server-side assemblies
  - For .NET Framework 4.X projects, use `ActualLab.Fusion.Server.NetFx` instead
- `ActualLab.Fusion.Blazor` &ndash; for Blazor applications
- `ActualLab.Fusion` &ndash; for shared assemblies (used on both client and server)
- `ActualLab.Fusion.EntityFramework` &ndash; for server-side assemblies using [EF Core](https://docs.microsoft.com/en-us/ef/)

## Packages

### Shared packages

- [ActualLab.Core](https://www.nuget.org/packages/ActualLab.Core/) &ndash; Base abstractions and helpers
- [ActualLab.Generators](https://www.nuget.org/packages/ActualLab.Generators/) &ndash; Generic Roslyn-based proxy type generator used by ActualLab.Rpc and Fusion
- [ActualLab.Interception](https://www.nuget.org/packages/ActualLab.Interception/) &ndash; Call interception API used by `ActualLab.Generators`
- [ActualLab.Core.Legacy](https://www.nuget.org/packages/ActualLab.Core.Legacy/) &ndash; Obsolete types moved out of `ActualLab.Core` (`OptionSet`, `ImmutableOptionSet`)

### ActualLab.Rpc

- [ActualLab.Rpc](https://www.nuget.org/packages/ActualLab.Rpc/) &ndash; The fastest RPC implementation for .NET; this package includes its WebSocket client
- [ActualLab.Rpc.Server](https://www.nuget.org/packages/ActualLab.Rpc.Server/) &ndash; RPC server for ASP.NET Core
- [ActualLab.Rpc.Server.NetFx](https://www.nuget.org/packages/ActualLab.Rpc.Server.NetFx/) &ndash; RPC server for .NET Framework
- [ActualLab.Rpc.TypeScript](https://www.nuget.org/packages/ActualLab.Rpc.TypeScript/) &ndash; Generates TypeScript contracts (interfaces, DTOs, service definitions) from RPC and Fusion services
- [ActualLab.Rpc.TypeScript.Generator](https://www.nuget.org/packages/ActualLab.Rpc.TypeScript.Generator/) &ndash; An MSBuild target running that generator on your contracts project after each build

### CommandR

- [ActualLab.CommandR](https://www.nuget.org/packages/ActualLab.CommandR/) &ndash; MediatR-like library that supports both interface-based and method-based handlers, with unified pipeline behaviors and minimal boilerplate

### Fusion

- [ActualLab.Fusion](https://www.nuget.org/packages/ActualLab.Fusion/) &ndash; Core Fusion abstractions
- [ActualLab.Fusion.Server](https://www.nuget.org/packages/ActualLab.Fusion.Server/) &ndash; Server-side Fusion and ActualLab.Rpc integration

### Database Integration

- [ActualLab.Fusion.EntityFramework](https://www.nuget.org/packages/ActualLab.Fusion.EntityFramework/) &ndash; Operations Framework / EF Core integration
- [ActualLab.Fusion.EntityFramework.Npgsql](https://www.nuget.org/packages/ActualLab.Fusion.EntityFramework.Npgsql/) &ndash; additional PostgreSQL extensions
- [ActualLab.Fusion.EntityFramework.Redis](https://www.nuget.org/packages/ActualLab.Fusion.EntityFramework.Redis/) &ndash; Redis additional Redis extensions

### Blazor Integration

- [ActualLab.Fusion.Blazor](https://www.nuget.org/packages/ActualLab.Fusion.Blazor/) &ndash; Blazor components (`ComputedStateComponent&lt;T&gt;`, etc.)
- [ActualLab.Fusion.Blazor.Authentication](https://www.nuget.org/packages/ActualLab.Fusion.Blazor.Authentication/) &ndash; Fusion Authentication for Blazor (`IAuth` support, `AuthenticationStateProvider` implementation, etc.)

### Fusion Extensions (`IAuth`, etc.)

- [ActualLab.Fusion.Ext.Contracts](https://www.nuget.org/packages/ActualLab.Fusion.Ext.Contracts/) &ndash; Contracts (client-side package)
- [ActualLab.Fusion.Ext.Backend.Contracts](https://www.nuget.org/packages/ActualLab.Fusion.Ext.Backend.Contracts/) &ndash; Backend contracts (`IAuthBackend` and its commands)
- [ActualLab.Fusion.Ext.Services](https://www.nuget.org/packages/ActualLab.Fusion.Ext.Services/) &ndash; Implementations (server-side package)

### Optional Serialization Packages

`ActualLab.Core` supports System.Text.Json and MessagePack. Other serializers come from separate
packages &ndash; see [Serializer Packages](./PartS.md#serializer-packages):

- [ActualLab.Serialization.NewtonsoftJson](https://www.nuget.org/packages/ActualLab.Serialization.NewtonsoftJson/) &ndash; Newtonsoft.Json support: `NewtonsoftJsonSerializer` and the `njson*` RPC formats. `ActualLab.Fusion.EntityFramework`, `ActualLab.Fusion.Ext.Services` and `ActualLab.Core.Legacy` reference it. See [Newtonsoft.Json](./PartS.md#newtonsoft-json).
- [ActualLab.Serialization.NerdbankMessagePack](https://www.nuget.org/packages/ActualLab.Serialization.NerdbankMessagePack/) &ndash; Nerdbank.MessagePack support: `NerdbankMessagePackByteSerializer` and the `nmsgpack*` RPC formats (.NET 8+). See [Nerdbank.MessagePack](./PartS.md#nerdbank-messagepack).
- [ActualLab.Serialization.MemoryPack](https://www.nuget.org/packages/ActualLab.Serialization.MemoryPack/) &ndash; MemoryPack support: `MemoryPackByteSerializer`, `ApiOption<T>` / `ApiNullable<T>` and the `mempack*` RPC formats. Not recommended outside of compatibility with pre-v16 peers. `ActualLab.Fusion.Ext.Contracts` references it (except on .NET Standard 2.0). See [MemoryPack](./PartS.md#memorypack).

## Package Dependencies

Direct dependencies of each package on other ActualLab packages (`ActualLab.Generators` is a build-time
dependency of most of them and isn't listed):

| Package | Depends on |
|---------|------------|
| `ActualLab.Core` | &ndash; |
| `ActualLab.Core.Legacy` | `ActualLab.Serialization.NewtonsoftJson` |
| `ActualLab.Interception` | `ActualLab.Core` |
| `ActualLab.Plugins` | `ActualLab.Core` |
| `ActualLab.Redis` | `ActualLab.Core` |
| `ActualLab.RestEase` | `ActualLab.Core` |
| `ActualLab.Testing` | `ActualLab.Core` |
| `ActualLab.Rpc` | `ActualLab.Interception` |
| `ActualLab.Rpc.Server` | `ActualLab.Rpc` |
| `ActualLab.Rpc.TypeScript` | `ActualLab.Rpc` |
| `ActualLab.Rpc.TypeScript.Generator` | `ActualLab.Fusion`, `ActualLab.Rpc.TypeScript` |
| `ActualLab.CommandR` | `ActualLab.Rpc` |
| `ActualLab.Fusion` | `ActualLab.CommandR` |
| `ActualLab.Serialization.NewtonsoftJson` | `ActualLab.Fusion` |
| `ActualLab.Serialization.NerdbankMessagePack` | `ActualLab.Fusion` |
| `ActualLab.Serialization.MemoryPack` | `ActualLab.Fusion` |
| `ActualLab.Fusion.Blazor` | `ActualLab.Fusion` |
| `ActualLab.Fusion.Server` | `ActualLab.Fusion.Blazor`, `ActualLab.Rpc.Server` |
| `ActualLab.Fusion.EntityFramework` | `ActualLab.Fusion`, `ActualLab.Serialization.NewtonsoftJson` |
| `ActualLab.Fusion.EntityFramework.Npgsql` | `ActualLab.Fusion.EntityFramework` |
| `ActualLab.Fusion.EntityFramework.Redis` | `ActualLab.Fusion.EntityFramework`, `ActualLab.Redis` |
| `ActualLab.Fusion.Ext.Contracts` | `ActualLab.Fusion`, `ActualLab.Serialization.MemoryPack` (not on .NET Standard 2.0) |
| `ActualLab.Fusion.Ext.Backend.Contracts` | `ActualLab.Fusion.Ext.Contracts` |
| `ActualLab.Fusion.Ext.Services` | `ActualLab.Fusion.Server`, `ActualLab.Fusion.EntityFramework`, `ActualLab.Fusion.Ext.Backend.Contracts` |
| `ActualLab.Fusion.Blazor.Authentication` | `ActualLab.Fusion.Blazor`, `ActualLab.Fusion.Ext.Contracts` |
| `ActualLab.Rpc.Server.NetFx` | `ActualLab.Rpc` |
| `ActualLab.Fusion.Server.NetFx` | `ActualLab.Fusion.Ext.Contracts`, `ActualLab.Rpc.Server.NetFx` |

So a client that references only `ActualLab.Fusion` (and, optionally, `ActualLab.Serialization.MemoryPack`)
doesn't get Newtonsoft.Json.

<br />

::: tip
For a complete list of all packages, visit:
https://www.nuget.org/packages?q=tags%3A%22actual_lab_fusion%22+Owner%3A%22Actual.chat%22
:::
