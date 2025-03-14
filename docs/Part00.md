# Part 0: NuGet Packages

All Fusion packages are
[available on NuGet](https://www.nuget.org/packages?q=tags%3A%22actual_lab_fusion%22+Owner%3A%22Actual.chat%22):\
[![Build](https://github.com/ActualLab/Fusion/workflows/Build/badge.svg)](https://github.com/ActualLab/Fusion/actions?query=workflow%3A%22Build%22)
[![NuGetVersion](https://img.shields.io/nuget/v/ActualLab.Core)](https://www.nuget.org/packages?q=tags%3A%22actual_lab_fusion%22+Owner%3A%22Actual.chat%22)

You should reference these packages based on your project type:

* `ActualLab.Fusion.Server` &ndash; for server-side assemblies
  * For .NET Framework 4.X projects, use `ActualLab.Fusion.Server.NetFx` instead
* `ActualLab.Fusion.Client` &ndash; for client-side assemblies
  * For Blazor applications, use `ActualLab.Fusion.Blazor` instead (it includes `ActualLab.Fusion.Client`)
* `ActualLab.Fusion` &ndash; for shared assemblies (used on both client and server)
* `ActualLab.Fusion.EntityFramework` &ndash; for server-side assemblies using [EF Core](https://docs.microsoft.com/en-us/ef/)

## Packages

### Shared packages
* [ActualLab.Core](https://www.nuget.org/packages/ActualLab.Core/) - Base abstractions and helpers
* [ActualLab.Generators](https://www.nuget.org/packages/ActualLab.Generators/) - Generic Roslyn-based proxy type generator used by ActualLab.Rpc and Fusion
* [ActualLab.Interception](https://www.nuget.org/packages/ActualLab.Interception/) - Call interception API used by `ActualLab.Generators`

### ActualLab.Rpc
* [ActualLab.Rpc](https://www.nuget.org/packages/ActualLab.Rpc/) - The fastest RPC implementation for .NET; this package includes its WebSocket client
* [ActualLab.Rpc.Server](https://www.nuget.org/packages/ActualLab.Rpc.Server/) - RPC server for ASP.NET Core
* [ActualLab.Rpc.Server.NetFx](https://www.nuget.org/packages/ActualLab.Rpc.Server.NetFx/) - RPC server for .NET Framework

### CommandR
* [ActualLab.CommandR](https://www.nuget.org/packages/ActualLab.CommandR/) - MediatR-like library that supports both interface-based and method-based handlers, with unified pipeline behaviors and minimal boilerplate

### Fusion
* [ActualLab.Fusion](https://www.nuget.org/packages/ActualLab.Fusion/) - Core Fusion abstractions
* [ActualLab.Fusion.Server](https://www.nuget.org/packages/ActualLab.Fusion.Server/) - Server-side Fusion and ActualLab.Rpc integration
* [ActualLab.Fusion.Client](https://www.nuget.org/packages/ActualLab.Fusion.Client/) - Client-side Fusion and ActualLab.Rpc integration

### Database Integration
* [ActualLab.Fusion.EntityFramework](https://www.nuget.org/packages/ActualLab.Fusion.EntityFramework/) - Operations Framework / EF Core integration
* [ActualLab.Fusion.EntityFramework.Npgsql](https://www.nuget.org/packages/ActualLab.Fusion.EntityFramework.Npgsql/) - additional PostgreSQL extensions
* [ActualLab.Fusion.EntityFramework.Redis](https://www.nuget.org/packages/ActualLab.Fusion.EntityFramework.Redis/) - Redis additional Redis extensions

### Blazor Integration
* [ActualLab.Fusion.Blazor](https://www.nuget.org/packages/ActualLab.Fusion.Blazor/) - Blazor components (`ComputedStateComponent&lt;T&gt;`, etc.)
* [ActualLab.Fusion.Blazor.Authentication](https://www.nuget.org/packages/ActualLab.Fusion.Blazor.Authentication/) - Fusion Authentication for Blazor (`IAuth` support, `AuthenticationStateProvider` implementation, etc.)

### Fusion Extensions (`IAuth`, etc.)
* [ActualLab.Fusion.Ext.Contracts](https://www.nuget.org/packages/ActualLab.Fusion.Ext.Contracts/) - Contracts (client-side package)
* [ActualLab.Fusion.Ext.Services](https://www.nuget.org/packages/ActualLab.Fusion.Ext.Services/) - Implementations (server-side package)

::: tip
For a complete list of all packages, visit:
https://www.nuget.org/packages?q=tags%3A%22actual_lab_fusion%22+Owner%3A%22Actual.chat%22
:::

#### [Next: Part 1 &raquo;](./Part01.md) | [Tutorial Home](./README.md)
