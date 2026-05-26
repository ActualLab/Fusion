# HTTP/2 Transport

`ActualLab.Rpc` defaults to WebSockets, but it also ships a **full-duplex
HTTP/2** transport built on a long-lived `POST` request whose request and
response bodies are read/written concurrently for the lifetime of the
connection. The wire protocol, message framing, handshake, keep-alives,
streaming, and reconnect logic are identical to the WebSocket transport —
only the underlying byte channel changes.

## When to use HTTP/2 instead of WebSockets

Both transports support the same feature set (regular calls, streams,
no-wait, reverse RPC). Pick based on the surrounding infrastructure:

| Scenario | Pick |
|----------|------|
| Default — bidirectional, low-latency, mature browser support | **WebSockets** |
| Existing HTTP/2 reverse-proxy / load balancer with no WS upgrade path | **HTTP/2** |
| Network paths where intermediate HTTP/1.1 proxies break WS upgrades | **HTTP/2** |
| Server-side .NET clients that already manage long-lived HTTP/2 pools (`SocketsHttpHandler`) | **HTTP/2** |
| Browser clients (no native HTTP/2 duplex API today) | **WebSockets** |

::: tip Performance
WebSockets are still the **faster** transport — roughly **~15% higher
throughput** than HTTP/2 in `RpcBenchmark` from
[Fusion Samples](https://github.com/ActualLab/Fusion.Samples).

The gap comes from both layers:

- **Wire format**: HTTP/2 adds framing overhead WebSocket doesn't — HPACK
  headers per stream, flow-control windows, `:status`/`:method`
  pseudo-headers, etc.
- **.NET plumbing**: `System.Net.WebSockets.WebSocket` is a comparatively
  thin wrapper over the OS `Socket` and feeds bytes straight into our
  frame codec. The HTTP/2 path goes through `HttpClient`'s
  `HttpContent.Stream` on the client and `PipeReader` / `PipeWriter` on
  the ASP.NET Core server — each layer is well-tuned in isolation, but
  the stack is taller, so per-message overhead is higher.

Pick HTTP/2 for the infrastructure reasons above; pick WebSockets when
nothing else forces your hand.
:::

The HTTP/2 transport is .NET-only: there is no browser client. If your
client is `dotnet`-hosted (MAUI, WASM via `ActualLab.Rpc`, console, server),
either transport works. See [Coexisting with WebSockets](#coexisting-with-websockets)
below for how to register both transports and switch between them.

## What HTTP/2 actually requires

Full-duplex needs the *same* HTTP request to read and write concurrently,
which is **HTTP/1.x impossible**. The client always sends a single
long-lived `POST` and:

- on **`https://`** URLs — sends `Version = HTTP/2`,
  `VersionPolicy = RequestVersionOrHigher`. The TLS layer negotiates HTTP/2
  via **ALPN**; HTTP/3 is acceptable too.
- on **`http://`** URLs — sends `Version = HTTP/2`,
  `VersionPolicy = RequestVersionExact`. This is **HTTP/2 cleartext (h2c)
  with prior knowledge** — there is no Upgrade dance, no ALPN, and no
  HTTP/1.1 fallback.

The server must accept HTTP/2 on the endpoint that receives the request.
`RpcHttpServer` rejects anything below HTTP/2 with `426 Upgrade Required`
when `MustRequireHttp2 = true` (the default).

## Server setup

Required NuGet packages: `ActualLab.Rpc.Server`.

```csharp
var builder = WebApplication.CreateBuilder();

// 1. Configure Kestrel for HTTP/2 — see "Kestrel protocols" below for the
//    two valid combinations (HTTPS + ALPN, or HTTP/2 cleartext).
builder.WebHost.ConfigureKestrel(kestrel => {
    kestrel.ConfigureEndpointDefaults(listen => {
        listen.Protocols = HttpProtocols.Http1AndHttp2; // ALPN-negotiated under TLS
    });
});

// 2. Register the RPC HTTP server alongside (or instead of) the WS server.
var rpc = builder.Services.AddRpc();
rpc.AddHttpServer();              // POST /rpc/http  + (optional) /backend/rpc/http
// rpc.AddWebSocketServer();      // optional — you can expose both transports

// 3. Map the endpoint.
var app = builder.Build();
app.UseRouting();
app.UseEndpoints(endpoints => {
    endpoints.MapRpcHttpServer(); // wires up the /rpc/http POST handler
});

await app.RunAsync();
```

`AddHttpServer()` also has a `bool exposeBackend` overload (or pass
`Action<RpcHttpServerBuilder>` to mutate `RpcHttpServerOptions`) — these
mirror `AddWebSocketServer()`.

### Kestrel protocols

Pick one of these two endpoint configurations:

**TLS + ALPN (recommended for production):**
```csharp
kestrel.ListenAnyIP(443, listen => {
    listen.Protocols = HttpProtocols.Http1AndHttp2;
    listen.UseHttps(/* certificate */);
});
```
A single endpoint serves regular HTTP/1.1 traffic and HTTP/2 RPC; ALPN at
TLS-handshake time tells Kestrel which protocol the client wants.

**HTTP/2 cleartext (h2c):**
```csharp
kestrel.ListenAnyIP(5001, listen => {
    listen.Protocols = HttpProtocols.Http2; // NOT Http1AndHttp2
});
```
There is no ALPN over cleartext, so the protocol cannot be negotiated —
**you must dedicate the listener to HTTP/2.** If you serve both HTTP/1.1
content and HTTP/2 RPC over plain TCP, you need **two listeners on two
ports**, one for each protocol. Putting HTTP/1.1 and HTTP/2 on the same
cleartext port produces a server that responds correctly to neither.

Cleartext is useful for behind-load-balancer / mesh setups where TLS
terminates upstream. The server typically still sits behind a proxy
(envoy / nginx / Cloud Load Balancer) that terminates TLS and forwards
h2c to the backend.

## Client setup

Required NuGet packages: `ActualLab.Rpc` (no separate `Http` package — the
HTTP/2 client lives in `ActualLab.Rpc.Clients`).

```csharp
var rpc = services.AddRpc();
rpc.AddHttpClient("https://rpc.example.com/"); // base URL
fusion.AddClient<IChatService>();              // typed proxy as usual
```

The default `ConnectionUriResolver` appends `/rpc/http?clientId=…&f=…` to
the host URL (matching the server's `RpcHttpServerOptions.RequestPath`).

### Cleartext clients

Use a plain `http://` URL and the client switches to *prior-knowledge*
HTTP/2 automatically:
```csharp
rpc.AddHttpClient("http://rpc.internal:5001/");
```

### Custom `HttpClientFactory`

For TLS validation overrides, proxies, request headers, or connection
pooling you usually replace the default `HttpClient` factory:

```csharp
services.AddSingleton<RpcHttpClientOptions>(c => RpcHttpClientOptions.Default with {
    HostUrlResolver = _ => "https://rpc.example.com/",
    HttpClientFactory = _ => new HttpClient(new SocketsHttpHandler {
        EnableMultipleHttp2Connections = true, // multiple concurrent peers per host
        // For self-signed certificates in dev / on-prem:
        SslOptions = new SslClientAuthenticationOptions {
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
        },
    }),
});
```

The default factory already sets `EnableMultipleHttp2Connections = true`
so a single `HttpClient` can fan out across peers without artificial
stream-multiplexing limits.

## Options

`RpcHttpClientOptions` and `RpcHttpServerOptions` largely mirror the
WebSocket pair. The two flags worth knowing:

| Option | Default | Meaning |
|--------|---------|---------|
| `MustRequireHttp2` (both) | `true` | Server returns `426 Upgrade Required` for non-HTTP/2 requests; client throws if the response version is below HTTP/2. Turn off only if you're tunnelling through something that strips version headers. |
| `UsePipes` (both) | `true` | Use `System.IO.Pipelines` (`RpcPipeTransport`) instead of raw `Stream` (`RpcStreamTransport`). Pipelines is faster and the default — switch off only for diagnostics or interop with components that don't speak pipelines. **Both peers must agree** since they read/write through the same channel. |

The connection-lifecycle timing limits (`ConnectTimeout`,
`KeepAliveTimeout`, etc.) come from `RpcLimits` and apply to both
transports uniformly — see [Configuration Options](./PartR-CO.md).

## Coexisting with WebSockets

You can register both:

```csharp
rpc.AddWebSocketServer();
rpc.AddHttpServer();
// …
endpoints.MapRpcWebSocketServer(); // GET /rpc/ws
endpoints.MapRpcHttpServer();      // POST /rpc/http
```

A given peer uses one transport for the life of its connection; the
choice is made on the client side by which of `AddWebSocketClient` /
`AddHttpClient` was registered. To switch dynamically, wire an
`RpcAlternatingClient` (in `ActualLab.Rpc.Clients`) over the two:
it tries each inner client in order on every reconnect attempt and skips
ones that have failed since the last successful connect. The default
policy is "any failure counts" — to refine it (e.g. only alternate on
specific error categories, or carry richer per-peer state), subclass
`RpcAlternatingClient` and override `CreateState()` to return a custom
`State` whose `IsFailed(clientPeer, connectionState)` encodes your rule.
For switching strategies that don't fit the alternation model at all
(weighted picks, region affinity, time-of-day routing), implement an
`RpcClient` subclass directly — `ConnectRemote` is the single virtual
method that needs to return an `RpcConnection`, and you can delegate to
any combination of `RpcWebSocketClient` / `RpcHttpClient` underneath.

## Common pitfalls

- **`net_http_invalid_response`-like errors on Kestrel cleartext** — most
  often a sign that the listener is `HttpProtocols.Http1AndHttp2`
  (without TLS) instead of `HttpProtocols.Http2`. Cleartext has no ALPN;
  Kestrel can't pick the protocol per-request.
- **Reverse proxy strips the `:status` pseudo-header / collapses to HTTP/1.1**
  — the client sees a downgrade and throws because `MustRequireHttp2` is on.
  Either fix the proxy (enable end-to-end HTTP/2) or disable the check.
- **Self-signed cert in dev** — `RemoteCertificateValidationCallback`
  via a custom `HttpClientFactory` (example above). Do not turn off
  validation in production.
- **Browser clients** — the browser `fetch` / `XMLHttpRequest` APIs don't
  expose the full-duplex stream this transport relies on; use the
  WebSocket transport from the browser regardless of whether the server
  also serves HTTP/2 RPC.
