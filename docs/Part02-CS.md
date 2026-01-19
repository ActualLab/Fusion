# RPC: Cheat Sheet

Quick reference for RPC setup and configuration.

## RPC Server Setup

Configure RPC server (ASP.NET Core):

```cs
var fusion = builder.Services.AddFusion();
fusion.AddServer<ICartService, CartService>();

// In Program.cs
app.MapRpcWebSocketServer(); // Maps /rpc/ws endpoint
```

Expose backend services:

```cs
fusion.AddServer<IInternalService, InternalService>(isBackend: true);

builder.Services.Configure<RpcWebSocketServerOptions>(o => {
    o.ExposeBackend = true; // Be careful with security!
});
```

## RPC Client Setup

Configure RPC client:

```cs
var fusion = services.AddFusion();
var rpc = fusion.Rpc;

rpc.AddWebSocketClient(new Uri("https://api.example.com"));
fusion.AddClient<ICartService>();
```

Custom host URL:

```cs
services.Configure<RpcWebSocketClientOptions>(o => {
    o.HostUrlResolver = peer => configuration["ApiUrl"];
});
```

## Compute Service Clients

Register client:

```cs
fusion.AddClient<ICartService>();
```

Use it:

```cs
// Call it like the original service.
// Cached results are used when available.
// Invalidations propagate from server to clients.
var orders = await cartService.GetOrders(cartId, cancellationToken);
```

## Common Configuration

Custom endpoint paths:

```cs
// Server
builder.Services.Configure<RpcWebSocketServerOptions>(o => {
    o.RequestPath = "/api/rpc";
    o.BackendRequestPath = "/internal/rpc"; // Must NOT be publicly exposed!
});

// Client (backend-to-backend only)
services.Configure<RpcWebSocketClientOptions>(o => {
    o.RequestPath = "/api/rpc";
    o.BackendRequestPath = "/internal/rpc"; // Must NOT be publicly exposed!
});
```

> **Warning:** `BackendRequestPath` must never be publicly exposed. It should only be accessible between backend services within your infrastructure (e.g., via internal network or service mesh).

Retry configuration:

```cs
services.Configure<RpcOutboundCallOptions>(o => {
    o.ReroutingDelays = RetryDelaySeq.Exp(0.5, 10); // 0.5s to 10s
});
```
