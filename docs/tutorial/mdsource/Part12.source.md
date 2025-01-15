# Part 12: ActualLab.Rpc in Fusion 6.1+

Fusion 6.1 brings a number of improvements and changes, and some these changes are breaking. Most notably, there is:
- `ActualLab.Rpc` - a new communication library, which is way more performant and efficient than the previous communication layer (HTTP + WebSockets). It doesn't require ASP.NET Core controllers and `IXxxClientDef` interfaces to work, so it's also way easier to use it.
- Compute & replica service registration methods in `FusionBuilder` and `CommanderBuilder` designed with `ActualLab.Rpc` in mind.

## So what is `ActualLab.Rpc`?

.NET offers a variety of RPC ("Remote Procedure Call") options:
- ASP.NET Core RESTful APIs
- gRPC - including its official .NET implementation
- SignalR
- [vs-StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc)
- etc.

So why did we even bother to build another one? Well, apparently, all these options aren't build with Fusion in mind :) Here is what we think is ideal to have in Fusion's case:
1. Any RPC call must run/retry unless it's explicitly cancelled. In other words, any RPC call must behave exactly as a regular call.
2. Above behavior should be extremely reliable - in particular, the calls must "go on" even if you're turning Airplane Mode on, getting disconnected, etc.
3. There must be a way to "extend" the implicit incoming call duration until the invalidation. This is a very specific aspect of Fusion calls: in fact, they complete when a) result invalidation happens b) the `Computed<T>` backing the call becomes available for GC.
4. Ideally, the RPC layer should be as efficient as possible. This implies no argument boxing, very "thin" middlewares, etc.
5. There must be a way to plug in ~ any binary serializers - most importantly, [MemoryPack](https://github.com/Cysharp/MemoryPack) and [MessagePack](https://github.com/MessagePack-CSharp/MessagePack-CSharp).
6. There must be a way to plug in caching layer in such a way that we can avoid double serialization there. In other words, we want to intercept specific binary messages (remote call request / result) which usually "live" on the lowest levels of RPC communication stack, and make them available for the caching layer to prevent it from serializing what's already serialized once more, to enable it to update the cache entry only when its value actually changed w/o serializing it, etc.

#1 to #3 are extremely difficult to resolve - e.g. previously we've managed to implement most of this via ASP.NET Core + WebSockets, but:
- ASP.NET Core controller pipeline is quite inefficient - it is at least 10x slower than gRPC or SignalR
- There is still no way to get #6 well.

We quickly concluded that if we want to implement all of this on top of any other transport (e.g. gRPC or SignalR), we'll effectively end up using it as a pure message delivery channel. In other words, all we need is an abstraction for a message delivery channel, which can be backed by any transport you like.

And this is exactly what `ActualLab.Rpc` is:
- An abstraction allowing you to share and consume remote services
- Which has all 6 properties listed above
- And uses `Channel<RpcMessage>` under the hood, so it is transport-independent.

The only transport implementation we have now is WebSockets, but we'll definitely add more options in future - e.g. [WebTransport](https://developer.mozilla.org/en-US/docs/Web/API/WebTransport) is certainly on our list. WebSockets were implemented first mainly due to their ubiquitous support plus the fact you can't block WebSocket connections running on top of HTTPS.

Besides that, `ActualLab.Rpc` is extremely fast. More likely than not it's the fastest transport available on .NET right now. We'll back this claim with some actual benchmarks later, but benchmarks of `ActualLab.Rpc` alone in Fusion test suite show that:
- It pushes through ~ 300,000 calls per second over a single local WebSocket connection and utilizing ~20% CPU on Ryzen Threadripper 3960X on this test
- This means the same server alone would serve ~ 3,000,000 RPC calls per second, assuming that typically `ActualLab.Rpc` client does slightly more than what server does for a given call, + we can scale the load to 100% by adding more WebSocket connections.

This is **125,000 RPS per core** (or 62,500, if we count virtual hyper-threaded cores). And nearly any gRPC benchmark you can find (e.g. [this one](https://www.nexthink.com/blog/comparing-grpc-performance)) shows that any number above 50K RPS is quite an achievement. 

And this is at least 10x higher RPS compared to RESTful APIs of pre-`ActualLab.Rpc` versions of Fusion.

## Can I use `ActualLab.Rpc` alone / without Fusion?

Absolutely.

### On both server and client sides:

#1. Reference `ActualLab.Rpc` NuGet package

#2. Register its services in `IServiceCollection`:
```
var rpc = services.AddRpc(); // returns RpcBuilder
```

### On the server side:

#1. Reference `ActualLab.Rpc.Server` NuGet package.

#2. Expose singleton services you want to call from the client:
```
rpc.AddServer<IMyService>();
// Some of alternatives:
rpc.AddServer<IMyService, MyService>(); // Expose IMyService resolved as MyService
rpc.AddServer<IMyService>("myService"); // Expose IMyService under "myService" name
```

See [RpcBuilder](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Rpc/RpcBuilder.cs) for other overloads of `AddServer`.

#3: Expose a WebSocket endpoint RPC clients will connect to:
```
rpc.AddWebSocketServer();
```
And assuming you use minimal ASP.NET Core API:
```
app.UseWebSockets(); // Adds WebSocket support to ASP.NET Core host
app.MapRpcWebSocketServer(); // Registers "/rpc/ws" endpoint
```

Note that `IMyService` above (as any other RPC service interface) must:
- Implement tagging `IRpcService` interface. This makes sure `ActualLab.Interception` proxy generator produces a proxy for it, which happens when you build the app (it's a Roslyn code generator).
- Have async methods. Any async method (i.e. returning `Task`, `Task<T>`, `ValueTask`, or `ValueTask<T>`) becomes remotely callable.

And any RPC service implementation (e.g. `MyService` above):
- Declares all RPC-callable methods as `virtual`.

### On the client side:

#1. Register clients of services you want to use:
```
rpc.AddClient<IMyService>(); // Adds a singleton IMyService, which is a client for this service
// Some of alternatives:
rpc.AddClient<IMyService>("myService"); // Consumes a IMyService named as "myService" on the server side
```

#2. Add a WebSocket client for `ActualLab.Rpc`:
```
rpc.AddWebSocketClient(serverUrl);
```

#3. Call client methods & get the results:
```
var myService = serviceProvider.GetRequiredService<IMyService>();
Console.WriteLine(await myService.Ping());
```

## What else ActualLab.Rpc can do?

Besides powering Fusion's client compute services (formerly - replica services), it also allows you to:
- Route calls to different servers based on call parameters - in other words, run a mesh of services & route calls to them transparently. See https://github.com/ActualLab/Fusion/tree/master/samples/MultiServerRpcApp
- Use server-side call routers - a service wrappers routing calls to either a local service implementation or an RPC client. This model allows any of your service shards on the server side consume data from any shard (including itself), but w/o triggering any RPC at all for the local data. Unfortunately, there is no sample for this yet.

#### [Part 13: Migration to Fusion 6.1+ &raquo;](./Part13.md) | [Tutorial Home](./README.md)
