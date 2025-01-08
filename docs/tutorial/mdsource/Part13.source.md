# Part 13: Migration to Fusion 6.1+


## Upgrade packages

**1.** Upgrade all Fusion package references to v6.1+.

**2.** Replace all references to `ActualLab.Fusion.Client` to `ActualLab.Fusion` - there is no separate client assembly in v6.1+, `ActualLab.Fusion.dll` contains the client.

**3.** If you are using Fusion authentication (e.g. `IAuth`, `User`, etc. - in fact, almost everything related to Fusion authentication except `Session`), reference:
- `ActualLab.Fusion.Ext.Contracts` - from your contracts / client-side projects
- `ActualLab.Fusion.Ext.Services` - from your server-side projects.

**4.** If you are using some projects declaring just `IXxxClientDef` types, remove these projects.

**5.** Any project that declares compute or command services must reference `ActualLab.Generators`.

**6.** Any project that declares `[MemoryPackable]` types must reference `MemoryPack.Generator`.


## Update compute/command services, commands, and other "wired" data types

**1.** Any compute service should implement `IComputeService`, any command service should implement `ICommandService`. 

These interfaces are just tagging ones, i.e. there are no any methods. `ActualLab.Generators` analyzers require them to generate proxies for such services.

Most likely you declare interfaces for any of such services to consume them on the client - and in this case the interface must be inherited from either `IComputeService` or `ICommandService`.

**2.** Decorate any non-primitive type which "travels" between the client & server with `MemoryPack` serialization attributes and make them `partial`. E.g. if you had:

```cs
// In pre-v6.1 such commands are typically nested into IXxxService
public record PostCommand(string Name, string Text) : ICommand<Unit>;
```

You should convert it to ~ this:

```cs
// 1. MemoryPack doesn't support nested types, so it has to be moved out of IXxxService; Rider/ReSharper has a refactoring for this, as for VS.NET, I am not sure.
// 2. All `[MemoryPackable]` types must be declared as `partial`
// 3. [MemoryPackable(GenerateType.VersionTolerant)] requires you to explicitly mark every serializable member with [MemoryPackOrder]
// 4. [DataContract] and [DataMember] are optional - you may want to have them if you end up using e.g. MessagePack serializer
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public partial record Chat_Post(
    [property: DataMember, MemoryPackOrder(0)] string Name,
    [property: DataMember, MemoryPackOrder(1)] string Text
    ) : ICommand<Unit>;
```

Note that any assembly which declares such types should reference `MemoryPack.Generator` package.

More details on `MemoryPack` serializer: https://github.com/Cysharp/MemoryPack


## Remove compute service controllers and `IXxxClientDef`-s

- Search for e.g. `[BasePath` or `[Get(` to find all `IXxxClientDef`-s


## Search and replace

Replace the following _complete_ words:
- `AddComputeService` -> `AddService`
- `AddCommandService` -> `AddService`
- `LatestNonErrorValue` -> `LastNonErrorValue`
- `LatestNonErrorComputed` -> `LastNonErrorComputed`
- `Session.Null` -> `null!`
- `ISessionProvider` -> `ISessionResolver` + maybe rename related variables/properties. `ISessionResolver` is the same as `ISessionProvider` in pre-v6.1, and old `ISessionResolver` is gone
- `BlazorModeController.IsServerSideBlazor(HttpContext)` -> `BlazorModeEndpoint.IsBlazorServer(HttpContext)`
- `MapFusionWebSocketServer` -> `MapRpcWebSocketServer`
- `MapFusionBlazorSwitch` -> `MapFusionBlazorMode`
- `AddBackendStatus` -> `AddRpcPeerStateMonitor`
- `StateHasChangedAsync` -> `NotifyStateHasChanged`


## Server-side changes

**1.** Remove old Fusion server setup logic.

**2.** Add the new one:
```cs
var fusion = services.AddFusion(RpcServiceMode.Server, true);
var fusionServer = fusion.AddWebServer();
```

The first line tells that any `FusionBuilder` (including `fusion`) we'll use further must publish any service registered via `AddService` call via `ActualLab.Rpc` - in other words, it "turns" any `AddService` call to `AddServer` call further.

The second call registers an RPC endpoint (`/rpc/ws`) and maps it to a WebSocket server from `ActualLab.Rpc.Server` package.

**3.** If you're using Fusion authentication, configure its endpoints like this:
```cs
fusionServer.ConfigureAuthEndpoint(_ => new() {
    DefaultSignInScheme = MicrosoftAccountDefaults.AuthenticationScheme,
    SignInPropertiesBuilder = (_, properties) => {
        properties.IsPersistent = true;
    }
});
fusionServer.ConfigureServerAuthHelper(_ => new() {
    NameClaimKeys = Array.Empty<string>(),
});
```

**4.** If you're using ASP.NET Core Minimal API, map additional endpoints:
```cs
endpoints.MapRpcWebSocketServer(); // Absolutely necessary
endpoints.MapFusionAuth(); // Optional - maps /signIn & /signOut endpoints
endpoints.MapFusionBlazorMode(); // Optional - maps /fusion/blazorMode/{isBlazorServer}
```

And if you prefer to use controllers for Fusion authentication & Blazor mode switch, add this instead:
```cs
fusionServer.AddMvc().AddControllers();
```

For the note, these controllers now call the same services as endpoints, i.e. performance-wise endpoints are slightly better.

**5.** If you're using `DbAuthService`, its server-side builder is now invoked differently.

It used to be called like this:
```cs
db.AddAuthentication<...>();
```

And now it has to be:
```cs
fusion.AddDbAuthService<AppDbContext, ...>();
```


**6.** If you're using `fusionAuth.js` script to open sign-in/sign-out popup in Blazor, update its location:
- Search for `_content/ActualLab.Fusion.Blazor/scripts/fusionAuth.js` - most likely you'll find it in `_Host.cshtml`
- Replace it with `_content/ActualLab.Fusion.Blazor.Authentication/scripts/fusionAuth.js`

As you might notice, this also means you need to reference `ActualLab.Fusion.Blazor.Authentication` package.

## Client-side changes

**1.** You must remove the old RestEase client. Search for `fusion.AddRestEaseClient(` and remove this call + any uses of its output **except** the ones w/ `AddReplicaService` call.

E.g. if you had this code:

```cs
var fusionClient = fusion.AddRestEaseClient();
fusionClient.ConfigureHttpClient((c, name, options) => {
    options.HttpClientActions.Add(client => client.BaseAddress = apiBaseUri);
});
fusionClient.ConfigureWebSocketChannel(c => new () {
    BaseUri = baseUri,
});
// Registering replica service
fusionClient.AddReplicaService<ICounterService, ICounterClientDef>();
```

Leave just this (we'll update it later):
```cs
fusionClient.AddReplicaService<ICounterService, ICounterClientDef>();
```

Finally, replace `fusionClient.AddReplicaService<TInterface, TClientDef>()` calls with `fusion.AddClient<TInterface>()`.

You can do this in two steps:
1. Replace `fusionClient.AddReplicaService` with `fusion.AddClient`
2. Remove the extra argument.

**2.** Add new `ActualLab.Rpc` WebSocket client:
```cs
fusion.Rpc.AddWebSocketClient(builder.HostEnvironment.BaseAddress);
```

**3.** If you are using Fusion authentication, add `IAuth` service client:
```cs
fusion.AddAuthClient();
```


## Blazor related changes

**1.** If you're using Fusion authentication, reference `ActualLab.Fusion.Blazor.Authentication` package. The authentication components are optional now, so they're extracted to dedicated assemblies.


**2.** To add Fusion's Blazor components / integrations, run the following calls:

```cs
var fusionBlazor = fusion.AddBlazor(); 

// Optional calls:

// If you're going to use Fusion's AuthenticationStateProvider, add the following call:
fusionBlazor.AddAuthentication(); // Must go somewhere after services.AddServerSideBlazor() on the server side!

// If you'd like to automatically bump up SessionInfo.LastSeenAt while the client uses Session:
fusionBlazor.AddPresenceReporter();
```

These calls must be executed:
- On the client side, if you use just WASM client
- On the server side, if you use Blazor Server
- On both sides, if you use both.

**3.** If you are using `<CascadingAuthState>`, you can make it to automatically start presence reporter like this now:
```xml
<CascadingAuthState UsePresenceReporter="true">
  ...
</CascadingAuthState>
```


## Other changes

**1.** Authentication and other optional extensions were moved to `ActualLab.Fusion.Ext.Contracts` and `ActualLab.Fusion.Ext.Services` packages - with corresponding namespace changes. So most likely you'll need to replace:
- `using ActualLab.Fusion.EntityFramework.Authentication` to `using ActualLab.Fusion.Authentication`; on the server side, you'll also need to add `using ActualLab.Fusion.Authentication.Services`.
- `using ActualLab.Fusion.EntityFramework.Extensions` (if you are using `IKeyValueStore`, etc.) to `using ActualLab.Fusion.Extensions`; on the server side, you'll also need to add `using ActualLab.Fusion.Extensions.Services`.


**2.** `TaskSource<T>` type is gone (its performance was arguable). Use `TaskCompletionSource<T>` instead. You can use `TaskCompletionSourceExt.New` instead of `TaskSource.New` - it behaves identically, but returns similarly configured `TaskCompletionSource` instead of `TaskSource`.


#### [Next: Epilogue &raquo;](./PartFF.md) | [Tutorial Home](./README.md)
