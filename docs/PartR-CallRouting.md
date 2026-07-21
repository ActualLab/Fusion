# Call Routing

ActualLab.Rpc supports flexible call routing, enabling scenarios like:
- **Sharding** &ndash; Route calls to specific servers based on a shard key
- **Load balancing** &ndash; Distribute calls across multiple backend servers
- **Affinity routing** &ndash; Route calls based on user ID, entity ID, or other attributes
- **Dynamic topology** &ndash; Handle server additions/removals without client restarts


## Core Concepts

### RouterFactory

The `RouterFactory` is the entry point for custom routing. It's configured via `RpcOutboundCallOptions`:

```cs
services.AddSingleton(_ => RpcOutboundCallOptions.Default with {
    RouterFactory = methodDef => args => {
        // Return RpcRef based on method and arguments
        return RpcRef.Default;
    }
});
```

The factory receives an `RpcMethodDef` and returns a function that maps `ArgumentList` to `RpcRef`. This two-level design allows you to:
1. Inspect the method definition once (outer function)
2. Make per-call routing decisions based on arguments (inner function)


### RpcRef

`RpcRef` identifies the target peer for a call:

| Type | Description |
|------|-------------|
| `RpcRef.Default` | The default remote peer (for single-server scenarios) |
| `RpcRef.Local` | Execute locally (bypass RPC) |
| `RpcRef.Loopback` | In-process loopback (for testing) |
| Custom `RpcRef` | Your own peer reference, optionally with a resettable route |

`RpcRef` instances are **stable**: there is one ref per logical target (shard, host, "default"),
so you can cache them forever and return them from routers with no extra ceremony.
When the target moves (e.g., a shard-map change), the ref itself doesn't change &mdash;
its **route** (see below) is reset instead.

#### RpcPeerRef: renamed to RpcRef in v14.1

Starting from v14.1, `RpcPeerRef` is replaced by the `RpcRef` + `RpcRoute` pair:

| Before v14.1 | v14.1+ |
|--------------|--------|
| `RpcPeerRef` | `RpcRef` (stable; one instance per logical target) |
| `RpcRouteState` | `RpcRoute` (one instance per route generation) |
| `RpcPeerRef.RouteState` | `RpcRef.Route` (re-minted via `CreateRoute()` on change) |
| `RpcPeerRefBuilder` (TS, `@actuallab/rpc`) | `RpcRefBuilder` (TS refs stay plain strings; no route concept) |
| `RpcRouteStateExt` | Merged into `RpcRoute` (`IsChanged`, `RerouteIfChanged`, etc.) |
| `RpcPeer.Ref.RouteState` | `RpcPeer.Route` (the generation the peer is bound to) |

The key semantic change: refs used to be re-created on every topology change, so caches
had to version and re-mint them; now the ref is permanent and only its route is reset.


### RpcRoute

`RpcRoute` represents one *route generation* of an `RpcRef`: it carries the resolved target
info for that generation and signals when the route changes. A ref becomes reroutable by
overriding `CreateRoute()`:

```cs
public class MyPeerRef : RpcRef
{
    public MyPeerRef(string targetId)
    {
        HostInfo = targetId;
        Initialize(); // Mints the first route via CreateRoute()
    }

    protected override RpcRoute CreateRoute()
    {
        var route = new MyRoute(this); // Resolves the current target
        // Start monitoring for topology changes; each generation gets its own watcher
        _ = Task.Run(async () => {
            await WaitForTopologyChange(route.ChangedToken);
            route.MarkChanged(); // Triggers rerouting
        });
        return route;
    }
}
```

- `Initialize()` calls `CreateRoute()` once; the base implementation returns
  `RpcRoute.NewStatic(this)` &mdash; a *static* route (`IsStatic == true`) that is never
  marked as changed, so no rerouting logic is engaged (that's what plain client and
  server refs use).
- The `RpcRef.Route` getter re-mints **lazily**: when the current route is marked as changed,
  the next read invokes `CreateRoute()` for the next generation. A burst of topology churn
  with no interleaved calls therefore coalesces into a single re-resolution.
- `RpcRef.Reset()` marks the current route as changed and eagerly mints the next one.
- Each `RpcPeer` captures the route it was created for in `RpcPeer.Route`, so a draining
  peer keeps observing *its own* generation while `ref.Route` already points to the next one.

When `route.MarkChanged()` is called:
1. The `RpcPeer` bound to that route generation is disposed
2. Active calls on that peer receive `RpcRerouteException`
3. The RPC interceptor catches the exception
4. After a delay (`ReroutingDelays`), the call is rerouted via `RouterFactory`,
   which resolves the same stable ref &mdash; now with a freshly minted route


### RpcRerouteException

`RpcRerouteException` signals that a call must be rerouted to a different peer. It's thrown automatically when:
- `MarkChanged()` is called on the route of the peer serving the call
- An inbound call arrives at a server that's no longer responsible for the shard/entity

```cs
// Throwing manually (e.g., in a service method)
throw RpcRerouteException.MustReroute("Target server changed");
```


## Simple Example: Hash-Based Routing

This example from the `MultiServerRpc` sample routes chat calls based on chat ID hash:

```cs
const int serverCount = 2;
var serverUrls = Enumerable.Range(0, serverCount)
    .Select(i => $"http://localhost:{22222 + i}/")
    .ToArray();
var clientPeerRefs = serverUrls
    .Select(url => RpcRef.NewClient(url))
    .ToArray();

services.AddSingleton(_ => RpcOutboundCallOptions.Default with {
    RouterFactory = methodDef => args => {
        if (methodDef.Service.Type == typeof(IChat)) {
            var arg0Type = args.GetType(0);
            int hash;
            if (arg0Type == typeof(string))
                hash = args.Get<string>(0).GetXxHash3();
            else if (arg0Type == typeof(Chat_Post))
                hash = args.Get<Chat_Post>(0).ChatId.GetXxHash3();
            else
                throw new NotSupportedException("Can't route this call.");

            return clientPeerRefs[hash.PositiveModulo(serverCount)];
        }
        return RpcRef.Default;
    }
});
```

Key points:
- Routes `IChat` calls based on the first argument (chat ID or command)
- Uses `GetXxHash3()` for consistent hashing (doesn't change between runs)
- Falls back to `RpcRef.Default` for other services


## Advanced Example: Dynamic Mesh Routing

The `MeshRpc` sample demonstrates dynamic routing with automatic rerouting when topology changes.

### Custom stable PeerRef with a resettable route

```cs
public sealed class RpcShardRef : RpcRef
{
    private static readonly ConcurrentDictionary<ShardRef, RpcShardRef> Cache = new();

    public ShardRef ShardRef { get; }

    public static RpcShardRef Get(ShardRef shardRef)
        => Cache.GetOrAdd(shardRef, static key => new RpcShardRef(key));

    private RpcShardRef(ShardRef shardRef)
    {
        ShardRef = shardRef;
        HostInfo = shardRef.ToString();
        UseReferentialEquality = true;
        Initialize(); // Mints the first RpcShardRoute
    }

    protected override RpcRoute CreateRoute()
        => new RpcShardRoute(this);
}

public sealed class RpcShardRoute : RpcRoute
{
    public string HostId { get; } // The resolved target of this route generation

    internal RpcShardRoute(RpcShardRef rpcRef) : base(rpcRef)
    {
        var meshState = MeshState.State.Value;
        HostId = meshState.GetShardHost(rpcRef.ShardRef)?.Id ?? "null";

        // Monitor for topology changes; the watcher dies with its route generation
        _ = Task.Run(async () => {
            var computed = MeshState.State.Computed;
            // Wait until this host is removed from the mesh
            await computed.When(x => !x.HostById.ContainsKey(HostId), ChangedToken);
            MarkChanged(); // Triggers rerouting; the next Route read mints a new RpcShardRoute
        });
    }

    // Makes the route render as "<address> [vN->hostId]" in logs
    protected override string GetTargetString()
        => HostId;
}
```

Note that the ref itself carries only the stable shard identity; the per-generation
resolution (`HostId`) lives on the route. The cache is a plain `GetOrAdd` &mdash; no
versioning, no `IsChanged` double-checks, no re-minting races in app code.

### RouterFactory with Type-Based Routing

```cs
public Func<ArgumentList, RpcRef> RouterFactory(RpcMethodDef methodDef)
    => args => {
        if (args.Length == 0)
            return RpcRef.Local;

        var arg0Type = args.GetType(0);

        // Route by HostRef
        if (arg0Type == typeof(HostRef))
            return RpcHostRef.Get(args.Get<HostRef>(0));
        if (typeof(IHasHostRef).IsAssignableFrom(arg0Type))
            return RpcHostRef.Get(args.Get<IHasHostRef>(0).HostRef);

        // Route by ShardRef
        if (arg0Type == typeof(ShardRef))
            return RpcShardRef.Get(args.Get<ShardRef>(0));
        if (typeof(IHasShardRef).IsAssignableFrom(arg0Type))
            return RpcShardRef.Get(args.Get<IHasShardRef>(0).ShardRef);

        // Route by hash of first argument
        if (arg0Type == typeof(int))
            return RpcShardRef.Get(ShardRef.New(args.Get<int>(0)));

        return RpcShardRef.Get(ShardRef.New(args.GetUntyped(0)));
    };
```


## Rerouting Flow

When a peer's route state changes, the following sequence occurs:

<img src="/img/diagrams/PartR-CallRouting-1.svg" alt="Rerouting Flow" style="width: 100%; max-width: 800px;" />


## Local Execution Mode

When a call routes to the local server (via `RpcRef.Local` or a custom peer ref pointing to the current host), `RpcLocalExecutionMode` controls how local execution coordinates with rerouting signals.

This is only relevant for **distributed services** (`RpcServiceMode.Distributed`). Non-distributed services ignore this setting.

### RpcLocalExecutionMode Values

| Mode | LocalExecutionAwaiter | Rerouting Check | Cancellation Token | Use Case |
|------|----------------------|-----------------|-------------------|----------|
| `Unconstrained` | Not awaited | None | Original token | Non-distributed services, simple calls |
| `ConstrainedEntry` | Awaited once | At entry point only | Original token | Compute services, where late reroutes are acceptable |
| `Constrained` | Awaited | At entry + during execution | Linked to `RpcRoute.ChangedToken` | Long-running calls that must abort on reroute |

### How It Works

When a call executes locally with an `RpcRoute`:

1. **Unconstrained**: Executes immediately without coordination. Use for calls where rerouting mid-execution is acceptable.

2. **ConstrainedEntry**: Waits for `LocalExecutionAwaiter` before starting. If the route changed while waiting, throws `RpcRerouteException`. Once execution starts, it won't be interrupted.

3. **Constrained**: Same as `ConstrainedEntry`, plus the cancellation token is linked to `RpcRoute.ChangedToken`. If the route changes during execution, the call is cancelled and rerouted. This is the most defensive mode.

### Default Modes

The default mode depends on both the **service mode** and **method type**:

| Service Mode | Method Type | Default Mode | Rationale |
|--------------|-------------|--------------|-----------|
| `Distributed` | Regular methods | `Constrained` | Commands may have side effects; must abort if route changes |
| `Distributed` | Compute methods | `ConstrainedEntry` | Read-only; safe to complete locally even if route changes |
| Non-distributed | Any | `Unconstrained` | No rerouting concerns |

**Why compute methods use `ConstrainedEntry`:**

Compute methods (methods returning `Task<T>` on `IComputeService`) are read-only operations that produce cached computed values. If a compute method starts executing locally and the route changes mid-execution:
- The client (i.e., another server that requests the value) will notice the change in topology and terminate the peer responsible for the call, which triggers rerouting of all its open calls and invalidation of compute method calls awaiting invalidation.
- So at worst, a redundant computation occurs on the "wrong" (old) server.

Using `Constrained` would unnecessarily cancel the computation, which isn't much better than a subsequent rerouting.

**Why regular distributed methods use `Constrained`:**

Regular methods on distributed services often perform commands with side effects (database writes, state mutations). If a route changes mid-execution:
- The operation might complete on a server no longer responsible for the data
- This could cause inconsistencies in a sharded system
- Aborting and rerouting ensures the correct server handles the operation

**Resolution order:**

1. Method-level `[RpcMethod(LocalExecutionMode = ...)]` attribute
2. Service-level configuration via `HasLocalExecutionMode()`
3. Auto-default based on service mode and method type

### Configuration

Configure at the **service level**:

```cs
services.AddRpc()
    .AddDistributed<IMyService, MyServiceImpl>()
    .HasLocalExecutionMode(RpcLocalExecutionMode.ConstrainedEntry);
```

Override at the **method level** using `RpcMethodAttribute`:

```cs
public interface IMyService : IRpcService
{
    // Use Unconstrained for this specific fast method
    [RpcMethod(LocalExecutionMode = RpcLocalExecutionMode.Unconstrained)]
    Task<int> GetCount(string key, CancellationToken ct);

    // Use full Constrained for this long-running method
    [RpcMethod(LocalExecutionMode = RpcLocalExecutionMode.Constrained)]
    Task<Report> GenerateReport(ReportRequest request, CancellationToken ct);
}
```

### When to Use Each Mode

- **Unconstrained**: For fast, idempotent operations where executing on the "wrong" server temporarily is acceptable. Also used internally for NoWait calls and system calls.

- **ConstrainedEntry**: For compute methods or operations that are safe to complete locally even if the route changes mid-execution. The result may be discarded, but no side effects occur.

- **Constrained**: For operations with side effects (database writes, external API calls) that must not complete on a server that's no longer responsible for the data. This ensures consistency during topology changes.


## Remote Execution Mode

While `RpcLocalExecutionMode` (above) governs how *local* execution coordinates with rerouting, `RpcRemoteExecutionMode` governs how *remote* outbound calls behave with respect to the underlying connection: whether they wait for the peer to be connected, whether they resume after a reconnect, and whether they survive a change in peer identity.

It's a `[Flags]` enum, so values can be combined with `|`.

### RpcRemoteExecutionMode Values

| Flag | Value | When set | When unset |
|------|-------|----------|------------|
| `AwaitForConnection` | `1` | Wait for the peer to connect if it's disconnected when the call is made | The call fails immediately if the peer isn't connected |
| `AllowReconnect` | `2` | Resend the call after reconnecting to the same peer | The call is aborted on disconnect |
| `AllowResend` | `6` (`4 \| AllowReconnect`) | Resend the call even after reconnecting to a *different* peer (implies `AllowReconnect`) | The call is aborted when peer identity changes |
| `Default` | `7` (`AwaitForConnection \| AllowResend`) | Wait, reconnect, and resend &mdash; the default for regular outbound calls | |

Special cases:

- **`NoWait` methods** always use `0` regardless of this setting &mdash; they're fire-and-forget by design.
- **Compute methods** must use `Default`. Any other value is rejected at registration time, because compute-call semantics rely on the call eventually reaching *some* responsible peer.

**Resolution order** (same as for `RpcLocalExecutionMode`):

1. Method-level `[RpcMethod(RemoteExecutionMode = ...)]` attribute
2. Service-level configuration via `HasRemoteExecutionMode()`
3. `RpcRemoteExecutionMode.Default`

### Configuration

Configure at the **service level**:

```cs
services.AddRpc()
    .AddClient<IMyService>()
    .HasRemoteExecutionMode(RpcRemoteExecutionMode.AwaitForConnection);
```

Override at the **method level** using `RpcMethodAttribute`:

```cs
public interface IMyService : IRpcService
{
    // Fail fast if disconnected; don't resend on reconnect
    [RpcMethod(RemoteExecutionMode = RpcRemoteExecutionMode.AwaitForConnection)]
    Task<Quote> GetLiveQuote(string symbol, CancellationToken ct);

    // Fire-and-forget: mode is forced to 0 regardless of the attribute
    Task<RpcNoWait> Ping();

    // Full default: wait, reconnect, and resend even across peer changes
    [RpcMethod(RemoteExecutionMode = RpcRemoteExecutionMode.Default)]
    Task<Report> GenerateReport(ReportRequest request, CancellationToken ct);
}
```

### TypeScript

The same enum &mdash; with identical numeric values &mdash; is available in the TypeScript client (`@actuallab/rpc`). It can be applied either through `defineRpcService(...)` or through the `@rpcMethod(...)` decorator:

```ts
import {
    defineRpcService, RpcRemoteExecutionMode, RpcType, rpcMethod, rpcService,
} from '@actuallab/rpc';

// Via defineRpcService
const MyServiceDef = defineRpcService('MyService', {
    getLiveQuote: {
        args: ['symbol'],
        remoteExecutionMode: RpcRemoteExecutionMode.AwaitForConnection,
    },
    generateReport: {
        args: ['request'],
        // remoteExecutionMode: RpcRemoteExecutionMode.Default is the default
    },
    ping: { args: [], returns: RpcType.noWait }, // always uses 0
});

// Or via decorators
@rpcService('MyService')
class MyServiceClient {
    @rpcMethod({ remoteExecutionMode: RpcRemoteExecutionMode.AwaitForConnection })
    getLiveQuote(symbol: string): Promise<Quote> { /* ... */ }
}
```

Flag values on the TS side match .NET exactly: `AwaitForConnection = 1`, `AllowReconnect = 2`, `AllowResend = 6`, `Default = 7`. `NoWait` methods are likewise forced to `0`.


## Configuration

### Rerouting Delays

Configure delays between rerouting attempts via `RpcOutboundCallOptions`:

```cs
services.AddSingleton(_ => RpcOutboundCallOptions.Default with {
    ReroutingDelays = RetryDelaySeq.Exp(0.1, 5), // 0.1s to 5s exponential backoff
});
```

The delay sequence uses exponential backoff to avoid overwhelming the system during topology changes.

### Host URL Resolution

When using custom `RpcRef` types, configure how to resolve the actual host URL.
Read the *peer's* route (`peer.Route`) rather than the ref's current one &mdash; the peer
must keep connecting to the target of its own route generation:

```cs
services.Configure<RpcWebSocketClientOptions>(o => {
    o.HostUrlResolver = peer => {
        if (peer.Route is RpcShardRoute rpcShardRoute) {
            var host = GetHostById(rpcShardRoute.HostId);
            return host?.Url ?? "";
        }
        return peer.Ref.HostInfo;
    };
});
```

### Connection Kind Detection

Detect whether a peer points to a local or remote target. The detector receives the
route generation the peer is created for (its ref is `route.Ref`); it's consulted only
when the route doesn't specify a `ConnectionKind` on its own:

```cs
services.Configure<RpcPeerOptions>(o => {
    o.ConnectionKindDetector = route => {
        if (route is RpcShardRoute rpcShardRoute)
            return rpcShardRoute.HostId == currentHostId
                ? RpcPeerConnectionKind.Local
                : RpcPeerConnectionKind.Remote;

        return route.Ref.ConnectionKind;
    };
});
```

Alternatively, when each process resolves routes only for itself, set
`RpcRoute.ConnectionKind` directly in `CreateRoute()` &mdash; it takes precedence
over the detector.


## Best Practices

1. **Cache PeerRefs** &ndash; Create and reuse `RpcRef` instances for the same routing key. Since refs are stable, a plain `ConcurrentDictionary.GetOrAdd` is all you need.

2. **Use consistent hashing** &ndash; Use `GetXxHash3()` or similar stable hash functions. `string.GetHashCode()` varies between runs.

3. **Handle topology changes gracefully** &ndash; Override `CreateRoute()` and call `MarkChanged()` on the route to automatically reroute when servers come and go.

4. **Monitor rerouting** &ndash; Rerouting is logged at Warning level. High rerouting rates may indicate topology instability.

5. **Consider local execution** &ndash; Return `RpcRef.Local` when the call can be handled by the current server to avoid network overhead.
