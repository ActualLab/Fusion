# Part 0: NuGet packages

All Fusion packages are
[available on NuGet](https://www.nuget.org/packages?q=tags%3A%22actual_lab_fusion%22+Owner%3A%22Actual.chat%22):\
[![Build](https://github.com/ActualLab/Fusion/workflows/Build/badge.svg)](https://github.com/ActualLab/Fusion/actions?query=workflow%3A%22Build%22)
[![NuGetVersion](https://img.shields.io/nuget/v/ActualLab.Core)](https://www.nuget.org/packages?q=tags%3A%22actual_lab_fusion%22+Owner%3A%22Actual.chat%22)

Your should reference:

* `ActualLab.Fusion.Server` &ndash; from server-side assemblies
  * If you use .NET Framework 4.X, reference `ActualLab.Fusion.Server.NetFx` instead
* `ActualLab.Fusion.Client` &ndash; from client-side assemblies;
  * Blazor clients may reference `ActualLab.Fusion.Blazor` instead,
    which references `ActualLab.Fusion.Client`
* `ActualLab.Fusion` &ndash; from shared assemblies,
  i.e. the ones to be used on both sides.
* `ActualLab.Fusion.EntityFramework` &ndash; from server-side assemblies,
  if you plan to use [EF Core](https://docs.microsoft.com/en-us/ef/).

The list of Fusion packages:

* [ActualLab.Core](https://www.nuget.org/packages/ActualLab.Core/) -
  a collection of relatively isolated abstractions and helpers we couldn't find in BCL.
* [ActualLab.Generators](https://www.nuget.org/packages/ActualLab.Generators/) - has no dependencies.
  It's a Roslyn-based code generation library focused on proxies / call interception.
  All Fusion proxies are implemented with it. 
* [ActualLab.Interception](https://www.nuget.org/packages/ActualLab.Interception/) - depends on `ActualLab.Core`.
  Implements a number of call interception helpers which are used by [ActualLab.Generators].
* [ActualLab.Rpc](https://www.nuget.org/packages/ActualLab.Rpc/) - depends on `ActualLab.Core`.
  An RPC API that Fusion uses to implement Compute Service Clients.
  It's probably the fastest RPC implementation over WebSockets that's currently available on .NET - even for plain RPC calls.
* [ActualLab.Rpc.Server](https://www.nuget.org/packages/ActualLab.Rpc.Server/) - depends on `ActualLab.Rpc`.
  An implementation of `ActualLab.Rpc` server for ASP.NET Core, which uses WebSockets.
* [ActualLab.Rpc.Server.NetFx](https://www.nuget.org/packages/ActualLab.Rpc.Server.NetFx/) - depends on `ActualLab.Rpc`.
  An implementation of `ActualLab.Rpc` server for ASP.NET / .NET Framework 4.X, which uses WebSockets.
* [ActualLab.CommandR](https://www.nuget.org/packages/ActualLab.CommandR/) - depends on `ActualLab.Core` and `ActualLab.Interception`.
  CommandR is "[MediatR](hhttps://github.com/jbogard/MediatR) on steroids" designed to support
  not only interface-based command handlers, but also AOP-style handlers written as
  regular methods. Besides that, it unifies command handler API (pipeline behaviors and handlers
  are the same there) and helps to eliminate nearly all boilerplate code you'd have otherwise.
* [ActualLab.Fusion](https://www.nuget.org/packages/ActualLab.Fusion/) - depends on `ActualLab.Core`, `ActualLab.Interception`, and `ActualLab.CommandR`.
  Nearly everything related to Fusion is there.
* [ActualLab.Fusion.Ext.Contracts](https://www.nuget.org/packages/ActualLab.Fusion.Ext.Contracts/) - depends on `ActualLab.Fusion`.
  Contracts for some handy extensions (ready-to-use Fusion services) - e.g. Fusion-based authentication is there.
* [ActualLab.Fusion.Ext.Services](https://www.nuget.org/packages/ActualLab.Fusion.Ext.Services/) - depends on `ActualLab.Fusion.Ext.Contracts` and `ActualLab.Fusion.EntityFramework`.
  Implementations of extension contracts from `ActualLab.Fusion.Ext.Contracts`.
* [ActualLab.Fusion.Server](https://www.nuget.org/packages/ActualLab.Fusion.Server/) - depends on `ActualLab.Fusion` and `ActualLab.Rpc`.
  Basically, Fusion + `ActualLab.Rpc.Server` + some handy server-side helpers.
* [ActualLab.Fusion.Server.NetFx](https://www.nuget.org/packages/ActualLab.Fusion.Server.NetFx/) -
  .NET Framework 4.X version of `ActualLab.Fusion.Server`.
* [ActualLab.Fusion.Blazor](https://www.nuget.org/packages/ActualLab.Fusion.Blazor/) - depends on `ActualLab.Fusion`.
  Provides Blazor-Fusion integration. Most importantly, there is `StatefulCompontentBase<TState>`,
  which allows to create auto-updating components which recompute their state once the data they consume
  from Fusion services changes.
* [ActualLab.Fusion.Blazor.Authentication](https://www.nuget.org/packages/ActualLab.Fusion.Blazor.Authentication/) - depends on `ActualLab.Fusion.Blazor` and `ActualLab.Fusion.Ext.Contracts`.
  Implements Fusion authentication-related Blazor components.
* [ActualLab.Fusion.EntityFramework](https://www.nuget.org/packages/ActualLab.Fusion.EntityFramework/) - depends on `ActualLab.Fusion`.
  Contains [EF Core](https://docs.microsoft.com/en-us/ef/) integrations for CommandR and Fusion.
* [ActualLab.Fusion.EntityFramework.Npgsql](https://www.nuget.org/packages/ActualLab.Fusion.EntityFramework.Npgsql/) -
  depends on `ActualLab.Fusion.EntityFramework`.  
  Contains [Npgsql](https://www.npgsql.org/) - based implementation of operation log change tracking.
  PostgreSQL has [`NOTIFY / LISTEN`](https://www.postgresql.org/docs/13/sql-notify.html)
  commands allowing to use it as a message queue, so if you use this database,
  you don't need a separate message queue to allow Fusion to notify peer hosts about
  operation log changes.
* [ActualLab.Fusion.EntityFramework.Redis](https://www.nuget.org/packages/ActualLab.Fusion.EntityFramework.Redis/) -
  depends on `ActualLab.Fusion.EntityFramework`.  
  Contains [Redis](https://redis.com/) - based implementation of operation log change tracking.

There are some other packages, but more likely than not you won't need them. 
The complete list can be found here (the packages with the most recent version aren't obsolete): 
- https://www.nuget.org/packages?q=tags%3A%22actual_lab_fusion%22+Owner%3A%22Actual.chat%22 


#### [Next: Part 1 &raquo;](./Part01.md) | [Tutorial Home](./README.md)
