# ActualLab.Rpc Configuration Options

This document describes the configuration options available in ActualLab.Rpc.

## Overview

ActualLab.Rpc provides several options classes for fine-tuning RPC behavior:

| Options Class | Purpose |
|---------------|---------|
| `RpcLimits` | Connection timeouts, keep-alive, object lifecycle |
| `RpcPeerOptions` | Peer creation and connection lifecycle |
| `RpcOutboundCallOptions` | Outbound call routing, timeouts, rerouting |
| `RpcInboundCallOptions` | Inbound call processing |
| `RpcDiagnosticsOptions` | Call tracing and logging |
| `RpcRegistryOptions` | Service and method registration |
| `RpcWebSocketClientOptions` | WebSocket client connections |
| `RpcWebSocketServerOptions` | WebSocket server endpoints |
| `RpcTestClientOptions` | Testing with in-memory channels |

## `RpcLimits`

Defines timeout and periodic limits for RPC connections, keep-alive, and object lifecycle. Registered as a singleton in DI and accessible via `RpcHub.Limits`.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ConnectTimeout` | `TimeSpan` | `10s` | Timeout for establishing a connection; reconnect starts if exceeded |
| `HandshakeTimeout` | `TimeSpan` | `10s` | Timeout for completing handshake; reconnect starts if exceeded |
| `PrematureDisconnectTimeout` | `TimeSpan` | `15s` | If a connection was alive for less than this duration, a graceful close is still treated as an error — bumping `ConnectionAttemptIndex` and applying reconnect backoff delay. This prevents rapid connect-disconnect cycles from resetting the backoff. |
| `KeepAlivePeriod` | `TimeSpan` | `10s` | Interval at which a peer sends keep-alive messages (which also report which remote objects are still alive) |
| `KeepAliveTimeout` | `TimeSpan` | `25s` | If no keep-alive is received within this period, the connection is dropped and reconnect starts. Sized to tolerate a ~15s server stall plus most of one keepalive cycle — the worst-case age of `LastKeepAliveAt` is `KeepAlivePeriod + stall_duration`. |
| `ObjectReleasePeriod` | `TimeSpan` | `10s` | Cycle time for checking `KeepAliveTimeout` and `ObjectReleaseTimeout` |
| `ObjectReleaseTimeout` | `TimeSpan` | `125s` | If an object doesn't receive a keep-alive for this long, it gets released |
| `ObjectAbortCycleCount` | `int` | `3` | Number of cycles to complete object abort (proceeds to next cycle if at least one object was disposed) |
| `ObjectAbortCyclePeriod` | `TimeSpan` | `1s` | Duration of a single object abort cycle |
| `CallAbortCyclePeriod` | `TimeSpan` | `1s` | Duration of a single call abort cycle |
| `CallCountLimit` | `int` | `int.MaxValue` | Backstop cap on `InboundCalls.Count + OutboundCalls.Count` per peer; the peer is reset when it's exceeded |
| `ObjectCountLimit` | `int` | `65536` | Backstop cap on `SharedObjects.Count + RemoteObjects.Count` per peer; the peer is reset when it's exceeded |
| `CallTimeoutCheckPeriod` | `RandomTimeSpan` | `5s ±20%` | How often call timeouts are checked |

When a debugger is attached, the defaults for `HandshakeTimeout` (60s), `KeepAlivePeriod` (300s), and `KeepAliveTimeout` (1000s) are relaxed to avoid false timeouts during debugging.

### Example

```csharp
services.AddSingleton(new RpcLimits(Debugger.IsAttached) {
    PrematureDisconnectTimeout = TimeSpan.FromSeconds(30),
    KeepAlivePeriod = TimeSpan.FromSeconds(10),
    KeepAliveTimeout = TimeSpan.FromSeconds(40),
});
```

### Per-Peer Resource Caps

`CallCountLimit` and `ObjectCountLimit` (both added in v14.2) are backstops against a peer that
opens calls or streams and never finishes them. Both are checked once per `ObjectReleasePeriod`
rather than per call, so the actual count may overshoot by up to one cycle's worth before the
peer is reset.

`CallCountLimit` defaults to `int.MaxValue`, i.e. **disabled**, and deliberately so: a Fusion
server legitimately retains one inbound call per live client subscription, so 100K+ open inbound
calls is normal operation rather than a leak. Set it only once you know your own ceiling. `NoWait`
calls are never registered in either tracker, so they're invisible to this cap &mdash; lowering it
does not throttle a `NoWait` flood.

`ObjectCountLimit` defaults to 65,536. Shared objects are released only after
`ObjectReleaseTimeout` of silence, so a peer that abandons streams hovers near the cap &mdash; and
therefore gets reset &mdash; roughly once per that timeout.

## `RpcPeerOptions`

Configures RPC peer creation, connection handling, and lifecycle management.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `UseRandomHandshakeIndex` | `bool` | `false` | Use random handshake index values. Set to `true` for testing handshake issues. |
| `PeerFactory` | `Func<...>` | Auto | Factory to create RpcPeer instances (RpcServerPeer or RpcClientPeer based on ref type) |
| `ConnectionKindDetector` | `Func<...>` | Uses `RpcRef.ConnectionKind` | Determines connection kind for a peer reference |
| `TerminalErrorDetector` | `Func<...>` | `RpcReconnectFailedException` | Determines if an exception requires disconnection |
| `ServerConnectionFactory` | `Func<...>` | Auto | Creates RpcConnection for server peers |
| `ServerPeerShutdownTimeoutProvider` | `Func<...>` | 33% of peer lifetime (3-15 min) | Shutdown timeout for server peers |
| `PeerRemoveDelayProvider` | `Func<...>` | 0ms (server), 5min (client) | Delay before removing peer from registry |

### Example

```csharp
services.AddRpc().Configure<RpcPeerOptions>(options => {
    // Enable random handshake index for testing
    options.UseRandomHandshakeIndex = true;

    // Custom terminal error detection
    options.TerminalErrorDetector = (peer, error) =>
        error is RpcReconnectFailedException or ConnectionRefusedException;
});
```

## `RpcOutboundCallOptions`

Configures outbound RPC call behavior including routing, timeouts, and rerouting.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ReroutingDelays` | `RetryDelaySeq` | `Exp(0.1, 5)` | Exponential backoff for rerouting delays (0.1s to 5s). See [Call Routing](./PartR-CallRouting.md). |
| `TimeoutsProvider` | `Func<...>` | Based on method kind | Provides `RpcCallTimeouts` for specific methods |
| `RouterFactory` | `Func<...>` | Routes to `RpcRef.Default` | Creates routers to select target peer. See [Call Routing](./PartR-CallRouting.md). |
| `ReroutingDelayer` | `Func<...>` | `Task.Delay()` | Async function to apply rerouting delays |
| `Hasher` | `Func<...>` | SHA256, 24-char Base64 | Hashes byte data for consistency checking |

### `RpcCallTimeouts`

Timeouts used by `TimeoutsProvider`:

| Property | Default | Description |
|----------|---------|-------------|
| `ConnectTimeout` | `TimeSpan.MaxValue` | Timeout for establishing connection |
| `RunTimeout` | `TimeSpan.MaxValue` | Timeout for call execution |
| `LogTimeout` | `30 seconds` | Timeout for logging results |

### Default Timeouts by Method Type

| Method Type | Connect Timeout | Run Timeout |
|-------------|-----------------|-------------|
| Debug (debugger attached) | Infinite | 300s |
| Query (API) | Infinite | Infinite |
| Command (API) | 1.5s | 10s |
| Query (Backend) | Infinite | Infinite |
| Command (Backend) | 300s | 300s |

### Example

```csharp
services.AddRpc().Configure<RpcOutboundCallOptions>(options => {
    // Custom rerouting delays
    options.ReroutingDelays = RetryDelaySeq.Exp(0.5, 10); // 0.5s to 10s

    // Custom timeout provider
    options.TimeoutsProvider = (hub, methodDef) => new RpcCallTimeouts {
        ConnectTimeout = TimeSpan.FromSeconds(5),
        RunTimeout = TimeSpan.FromSeconds(30),
    };

    // Custom router (e.g., for sharding or load balancing)
    // See PartR-CallRouting.md for detailed examples
    options.RouterFactory = methodDef => args => RpcRef.Default;
});
```

## `RpcInboundCallOptions`

Configures how inbound RPC calls are processed on the receiving end.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ContextFactory` | `Func<...>` | Creates `RpcInboundContext` | Factory to create context for handling incoming calls |

### Example

```csharp
services.AddRpc().Configure<RpcInboundCallOptions>(options => {
    // Custom context factory with additional setup
    options.ContextFactory = (peer, message, peerChangedToken) => {
        var context = new RpcInboundContext(peer, message, peerChangedToken);
        // Additional context setup...
        return context;
    };
});
```

## `RpcDiagnosticsOptions`

Configures diagnostics, call tracing, and logging behavior.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MustPropagateAmbientActivityContext` | `bool` | `true` | Propagate `Activity.Current` to outbound call headers even when no RPC client activity exists |
| `OpenCallMetricsPeriodProvider` | `Func<RpcPeer, TimeSpan>` | 5 minutes for server peers, 1 minute for client peers | Minimum interval between open-call table scans |
| `CallTracerFactory` | `Func<...>` | `RpcDefaultCallTracer` (server), `null` (client) | Factory to create call tracers |
| `CallLoggerFactory` | `Func<...>` | Filters system KeepAlive calls | Factory to create call loggers |

### Example

```csharp
services.AddRpc().Configure<RpcDiagnosticsOptions>(options => {
    // Custom call tracer
    options.CallTracerFactory = (hub, methodDef) =>
        new MyCustomCallTracer(methodDef);

    // Custom call logger that logs everything
    options.CallLoggerFactory = (hub, methodDef) =>
        new RpcCallLogger(hub, methodDef);
});
```

## `RpcRegistryOptions`

Configures RPC service and method definition creation.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ServiceDefFactory` | `Func<...>` | Creates `RpcServiceDef` | Factory to create service definitions |
| `MethodDefFactory` | `Func<...>` | Creates `RpcMethodDef` | Factory to create method definitions |
| `ServiceScopeResolver` | `Func<...>` | "Backend" or "Api" | Determines service scope |

### Example

```csharp
services.AddRpc().Configure<RpcRegistryOptions>(options => {
    // Custom service scope resolution
    options.ServiceScopeResolver = (hub, serviceType) =>
        serviceType.Name.StartsWith("IInternal") ? "Backend" : "Api";
});
```

## `RpcWebSocketClientOptions`

Configures WebSocket-based RPC client connections.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RequestPath` | `string` | `"/rpc/ws"` | WebSocket endpoint path for API calls |
| `BackendRequestPath` | `string` | `"/backend/rpc/ws"` | WebSocket endpoint path for backend calls. **Must NOT be publicly exposed!** |
| `SerializationFormatParameterName` | `string` | `"f"` | Query parameter for serialization format |
| `ClientIdParameterName` | `string` | `"clientId"` | Query parameter for client ID |
| `ReconnectProofCounterParameterName` | `string` | `"c"` | Query parameter for the reconnect proof counter |
| `ReconnectProofParameterName` | `string` | `"p"` | Query parameter for the reconnect proof itself |
| `UseAutoFrameDelayerFactory` | `bool` | `false` | Enable automatic frame delaying |
| `HostUrlResolver` | `Func<...>` | Uses `peer.Ref.HostInfo` | Resolves host URL from peer reference |
| `ConnectionUriResolver` | `Func<...>` | HTTP→WS conversion | Creates WebSocket connection URI |
| `WebSocketTransportOptionsFactory` | `Func<...>` | Auto | Creates RpcWebSocketTransport options |
| `WebSocketOwnerFactory` | `Func<...>` | `ClientWebSocket` | Creates WebSocket instances |
| `FrameDelayerFactory` | `Func<...>` | `None` | Frame delaying mechanism |

### Example

```csharp
services.AddRpc().Configure<RpcWebSocketClientOptions>(options => {
    // Custom endpoint paths
    options.RequestPath = "/api/rpc";
    options.BackendRequestPath = "/internal/rpc"; // Must NOT be publicly exposed!

    // Custom host URL resolution
    options.HostUrlResolver = peer => {
        // Load balancer logic, etc.
        return "https://api.example.com";
    };

    // Enable frame delaying for high-latency connections
    options.UseAutoFrameDelayerFactory = true;
});
```

> **Warning:** `BackendRequestPath` must never be publicly exposed. It should only be accessible between backend services within your infrastructure.

## `RpcWebSocketServerOptions`

Configures WebSocket-based RPC server endpoints.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ExposeBackend` | `bool` | `false` | Whether to expose backend services via WebSocket. **Use with caution!** |
| `RequestPath` | `string` | `"/rpc/ws"` | WebSocket endpoint path for API calls |
| `BackendRequestPath` | `string` | `"/backend/rpc/ws"` | WebSocket endpoint path for backend calls. **Must NOT be publicly exposed!** |
| `SerializationFormatParameterName` | `string` | `"f"` | Query parameter for serialization format |
| `ClientIdParameterName` | `string` | `"clientId"` | Query parameter for client ID |
| `ReconnectProofCounterParameterName` | `string` | `"c"` | Query parameter for the reconnect proof counter |
| `ReconnectProofParameterName` | `string` | `"p"` | Query parameter for the reconnect proof itself |
| `RequireReconnectProof` | `bool` | `false` | Require a valid reconnect proof from every client reconnecting to a live server peer |
| `OriginValidator` | `RpcWebSocketServerOriginValidator` | `RpcWebSocketServerOriginValidators.AllowAll` | Decides from `(server, context, origin)` whether the upgrade may proceed; rejects with 403 before any peer is created |
| `WarnOnUnvalidatedOrigin` | `bool` | `true` | Log one startup warning when nothing validates the upgrade request's `Origin` |
| `ConfigureWebSocket` | `RpcWebSocketServerAcceptContextFactory` | Empty context | Creates the WebSocket accept context per connection from `(server, context, rpcRef)` — e.g. to enable compression selectively (.NET 6+) |

### Example

```csharp
services.AddRpc().Configure<RpcWebSocketServerOptions>(options => {
    // Expose backend services (be careful with security!)
    options.ExposeBackend = true;

    // Custom endpoint paths (must match client)
    options.RequestPath = "/api/rpc";
    options.BackendRequestPath = "/internal/rpc"; // Must NOT be publicly exposed!
});
```

> **Warning:** `BackendRequestPath` must never be publicly exposed. Ensure this endpoint is only accessible within your internal network or via service mesh. If `ExposeBackend` is `true`, take extra care to secure this endpoint.

### `OriginValidator`

The WebSocket handshake is exempt from CORS and from preflight, so a CORS policy
does not protect the RPC endpoint. If connections carry an ambient credential —
a Fusion session cookie, most notably — any page the victim visits can otherwise
open one and speak RPC as the victim (*cross-site WebSocket hijacking*).

`RpcWebSocketServerOriginValidators` ships three ready-made validators:

| Validator | Behavior |
|---|---|
| `AllowAll` | Accepts any origin. The default, so nothing breaks on upgrade |
| `SameOrigin` | Accepts only an origin whose host and port match the request's `Host` header |
| `Allow(params string[] origins)` | Accepts only the listed origins |

```csharp
rpc.AddWebSocketServer().Configure(_ => RpcWebSocketServerOptions.Default with {
    OriginValidator = RpcWebSocketServerOriginValidators.SameOrigin,
});

// Or, when the client is served from elsewhere:
rpc.AddWebSocketServer().Configure(_ => RpcWebSocketServerOptions.Default with {
    OriginValidator = RpcWebSocketServerOriginValidators.Allow(
        "https://app.example.com",
        "capacitor://localhost", // Mobile WebViews send scheme-specific origins...
        "null"),                 // ...or a literal "null"
});
```

All three allow a request that carries **no** `Origin` header. Browsers always
send it on a handshake and page scripts cannot forge it (it is a forbidden
header), so only non-browser clients can omit it — and omitting it gains them
nothing, since the attack depends on the victim's browser attaching the victim's
cookie. Non-browser clients (the .NET `RpcWebSocketClient`, CLI tools, backend
peers) therefore keep working under `SameOrigin` and `Allow(...)`.

`SameOrigin` compares the origin's host and port against the request's `Host`
header, and deliberately ignores the scheme: behind a TLS-terminating proxy
`Request.Scheme` is `http` unless forwarded headers are configured, while the
browser still reports `https`. It rejects the opaque `null` origin and any
non-`http(s)` scheme — use `Allow(...)` for WebView origins.

> **Note:** ASP.NET Core's `WebSocketMiddleware` has its own, independent gate:
> when `WebSocketOptions.AllowedOrigins` is non-empty it returns **403** for a
> mismatched `Origin` before the endpoint runs (an empty list allows everything,
> and an absent `Origin` is allowed even when the list is non-empty). The two
> mechanisms compose — the middleware runs first. Fusion's own validator adds
> what the platform list cannot do: it works on OWIN (`ActualLab.Rpc.Server.NetFx`,
> which has no equivalent at all), it applies to the RPC endpoint alone rather
> than every WebSocket in the app, and it can express "same origin as this
> request" instead of a fixed list.

### `WarnOnUnvalidatedOrigin`

Both WebSocket servers (`ActualLab.Rpc.Server` and `ActualLab.Rpc.Server.NetFx`) log a single
warning from their constructor when nothing validates the `Origin` &mdash; i.e. when
`OriginValidator` is still `AllowAll` and, on ASP.NET Core, `WebSocketOptions.AllowedOrigins`
is empty as far as DI can see. The message links to this page.

Turn it off for a server whose connections carry **no** ambient credentials &mdash; a
backend-only endpoint, or one where every call authenticates itself:

```csharp
rpc.AddWebSocketServer().Configure(_ => RpcWebSocketServerOptions.Default with {
    WarnOnUnvalidatedOrigin = false,
});
```

Note that options passed straight to `app.UseWebSockets(new WebSocketOptions { … })` never reach
DI, so a host that set `AllowedOrigins` that way is still warned; turning the warning off is the
right answer there.

### `RequireReconnectProof`

The connect URL's `clientId` alone used to select which server peer a connection attaches to,
and the incumbent connection was disconnected before anything was verified. A `clientId` is
unguessable, but it travels in a URL &mdash; so it lands in proxy logs, browser history and
`Referer` chains, and whoever reads one could loop the request to keep the victim permanently
offline, or replay it and inherit the victim's server-side peer state.

Since v14.2 the server mints a per-peer CSPRNG secret and delivers it inside its
`RpcHandshake` (the new `Secret` member) &mdash; over the established connection, never in a
URL. Every subsequent connect then carries two query parameters:

| Param | Value |
|---|---|
| `c` | a monotonic counter, canonical decimal, incremented once per connect attempt |
| `p` | `Base64Url_NoPad(HMAC_SHA256(UTF8(secret), UTF8(clientId + "\n" + c)))` |

Verification (`RpcReconnectProof.TryVerify`) runs before the peer is looked up, before the
WebSocket is accepted and before any disconnect, so a failed proof is a bare **403**: no socket
accepted, no peer created, incumbent untouched. All three server endpoints &mdash; ASP.NET Core
WebSocket, HTTP/2 and OWIN &mdash; share that one policy function, so they can't drift apart.

`RequireReconnectProof` defaults to `false`, and the gate still protects by default:

- An **unknown** `clientId` needs no proof &mdash; there's nothing to hijack yet, and it's what a
  client reaching a different replica legitimately sends.
- A `clientId` whose peer has **never** seen a valid proof takes the legacy path, so genuinely
  old clients keep working.
- A peer that **has** proven possession at least once may not downgrade: an absent `c`/`p` pair
  is refused regardless of this option, because that is exactly how an attacker would strip the
  proof. A new client is therefore covered from its second connect onwards.
- A present-but-invalid pair is always rejected.

Set it to `true` only once every client that can still reconnect to a live server peer speaks the
protocol, and only behind sticky routing: a reconnect that lands on a replica which already holds
a peer for that `clientId` &mdash; but issued a different secret &mdash; is rejected until that
peer expires (3&ndash;15 min). A host with a custom `ConnectionUriResolver` that supplies a
persistent `clientId` must not enable it unless it persists the secret too. The option is
bindable from configuration, so it can be rolled back without a redeploy.

The client side is automatic: `RpcWebSocketClient` appends `c` and `p` as soon as it holds a
secret. See [Reconnect proof](./PartTS-Rpc.md#reconnect-proof) for the TypeScript client, which
implements the same construction and degrades to the legacy URL where `crypto.subtle` is
unavailable.

## `RpcTestClientOptions`

Configures test client for RPC testing with in-memory channels.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SerializationFormatKey` | `string` | `""` | Serialization format identifier |
| `ChannelOptions` | `ChannelOptions` | BoundedChannelOptions(500) | Configuration for test message channels |
| `ConnectionFactory` | `Func<...>` | Twisted channel pair | Factory to create test channel pairs |

### Example

```csharp
// In tests
services.AddRpc().Configure<RpcTestClientOptions>(options => {
    // Use specific serialization format for testing
    options.SerializationFormatKey = "json";
});
```

## Configuration Patterns

### Basic Configuration

```csharp
var services = new ServiceCollection();
services.AddRpc()
    .Configure<RpcPeerOptions>(o => { /* ... */ })
    .Configure<RpcOutboundCallOptions>(o => { /* ... */ })
    .Configure<RpcWebSocketClientOptions>(o => { /* ... */ });
```

### Server-Side Configuration

```csharp
builder.Services.AddRpc()
    .Configure<RpcWebSocketServerOptions>(options => {
        options.ExposeBackend = false; // Security: don't expose internal services
        options.RequestPath = "/rpc/ws";
    });

// In middleware pipeline
app.MapRpcWebSocketServer();
```

### Client-Side Configuration

```csharp
services.AddRpc()
    .Configure<RpcWebSocketClientOptions>(options => {
        options.HostUrlResolver = peer => configuration["ApiUrl"];
    })
    .Configure<RpcOutboundCallOptions>(options => {
        options.ReroutingDelays = RetryDelaySeq.Exp(1, 30); // Longer rerouting delays
    });
```
