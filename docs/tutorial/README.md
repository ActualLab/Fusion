# Fusion Tutorial

> All project updates are published on [Fusion Place]; it's also the best place for Q/A.\
> [![Build](https://github.com/ActualLab/Fusion/workflows/Build/badge.svg)](https://github.com/ActualLab/Fusion/actions?query=workflow%3A%22Build%22)
> [![NuGetVersion](https://img.shields.io/nuget/v/ActualLab.Core)](https://www.nuget.org/packages?q=tags%3A%22actual_lab_fusion%22+Owner%3A%22Actual.chat%22)
> [![Fusion Place](https://img.shields.io/badge/Fusion%20%40%20Actual%20Chat-BE145B)](https://actual.chat/chat/s-1KCdcYy9z2-uJVPKZsbEo)

Welcome to [Fusion] Tutorial! It used to be interactive, but thanks to Microsoft's inability to maintain `dotnet try` tool, this nice feature currently doesn't work.

## Tutorial

* [QuickStart: Learn 80% of Fusion by walking through HelloCart sample](./QuickStart.md)
* [Part 0: NuGet packages](./Part00.md)
* [Part 1: Compute Services](./Part01.md)
* [Part 2: Computed Values: Computed&lt;T&gt;](./Part02.md)
* [Part 3: State: IState&lt;T&gt; and Its Flavors](./Part03.md)
* [Part 4: Compute Service Clients](./Part04.md)
* [Part 5: Fusion on Server-Side Only](./Part05.md)
* [Part 6: Real-time UI in Blazor Apps](./Part06.md)
* [Part 7: Real-time UI in JS / React Apps](./Part07.md)
* [Part 8: Scaling Fusion Services](./Part08.md)
* [Part 9: CommandR](./Part09.md)
* [Part 10: Multi-Host Invalidation and CQRS with Operations Framework](./Part10.md)
* [Part 11: Authentication in Fusion](./Part11.md) 
* <img src="https://img.shields.io/badge/-New!-brightgreen" valign="middle"> [Part 12: ActualLab.Rpc in Fusion 6.1+](./Part12.md) 
* <img src="https://img.shields.io/badge/-New!-brightgreen" valign="middle"> [Part 13: Migration to Fusion 6.1+](./Part13.md) 
* [Epilogue](./PartFF.md)
  
Finally, check out:
- <img src="https://img.shields.io/badge/-New!-brightgreen" valign="middle"> [Fusion Cheat Sheet](./Fusion-Cheat-Sheet.md) - consider adding it to Favorites :)
- [Overview](https://github.com/ActualLab/Fusion/blob/master/docs/Overview.md) - a high-level description of Fusion abstractions.

Join [Fusion Place] to ask questions and track project updates.

## Running Tutorial

**NOTE: Currently you can't run the Tutorial**, and Microsoft is the one to blame: they don't maintain `dotnet try` tool, which still targets only .NET Core 3.1. But if you want, you can still *try*.

Running with [Docker]:

- Install and [Docker Compose](https://docs.docker.com/compose/install/)
- Run `docker-compose up --build tutorial` in the root folder of this repository
- Open https://localhost:50005/README.md.

Alternatively, you can run it with `dotnet try` CLI tool:

- Install **both**
  [.NET 8.0 Preview SDK](https://dotnet.microsoft.com/download) and
  [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core)
- Install [Try .NET](https://github.com/dotnet/try/blob/master/DotNetTryLocal.md).
  If its release version fails to run the code, install its preview version.
- Run `dotnet try --port 50005 docs/tutorial` in the root folder of this repository
- Open https://localhost:50005/README.md.


[Fusion]: https://github.com/ActualLab/Fusion
[Fusion Place]: https://actual.chat/chat/s-1KCdcYy9z2-uJVPKZsbEo
[Fusion Feedback Form]: https://forms.gle/TpGkmTZttukhDMRB6
[Try .NET]: https://github.com/dotnet/try/blob/master/DotNetTryLocal.md
[Docker]: https://www.docker.com/
