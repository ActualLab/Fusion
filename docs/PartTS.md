# TypeScript Port

Fusion's TypeScript port brings the same reactive programming model to the JavaScript/TypeScript ecosystem.
It is designed to interoperate with the .NET Fusion server over `ActualLab.Rpc`,
giving you real-time, invalidation-driven UI updates in React (or any other JS framework) &mdash;
the same way Fusion + Blazor works on .NET.

::: tip
This section assumes you are familiar with Fusion's core concepts
([Compute Services](./PartF.md), [Computed\<T\>](./PartF-C.md), [States](./PartF-ST.md),
[ActualLab.Rpc](./PartR.md)).
It focuses on the TypeScript API surface and key differences from the .NET version.
:::


## npm Packages

| Package | Description | .NET Counterpart |
|---------|-------------|------------------|
| `@actuallab/core` | Core primitives: `Result`, `AsyncContext`, `AsyncLock`, `PromiseSource`, events | `ActualLab.Core` |
| `@actuallab/fusion` | `Computed<T>`, `@computeMethod`, `ComputedState`, `MutableState`, `UIActionTracker` | `ActualLab.Fusion` |
| `@actuallab/rpc` | `RpcHub`, `RpcClientPeer`, `RpcStream`, decorators, WebSocket transport | `ActualLab.Rpc` |
| `@actuallab/fusion-rpc` | `FusionHub` &mdash; compute-aware RPC with automatic invalidation propagation | `ActualLab.Fusion` (client part) |
| `@actuallab/fusion-react` | React hooks: `useComputedState`, `useMutableState` | `ActualLab.Fusion.Blazor` |

All packages are **ESM-first** (with CJS fallback), MIT-licensed, built with `tsup`, and tested with `vitest`.


## Architecture Overview

The typical setup mirrors Fusion + Blazor, but with React on the client:

```
┌─────────────────────────────┐     WebSocket      ┌────────────────────────────┐
│  TypeScript Client (React)  │ <=================> │  .NET Server (Fusion)      │
│                             │   ActualLab.Rpc     │                            │
│  FusionHub + RpcClientPeer  │                     │  Compute Services          │
│  Compute Service Clients    │  ← invalidation ──  │  Computed<T> cache         │
│  @computeMethod local cache │                     │  ActualLab.Rpc server      │
│  useComputedState (React)   │                     │                            │
└─────────────────────────────┘                     └────────────────────────────┘
```

1. **`FusionHub`** manages the RPC connection and creates typed client proxies
2. Client proxies route calls through **`ComputeFunction`** for local caching + dependency tracking
3. When the server invalidates a `Computed<T>`, it sends `$sys-c.Invalidate` to the client
4. The client invalidates its local replica, which cascades to any dependent `ComputedState`
5. React re-renders via `useComputedState`


## Quick Start

```ts
import { FusionHub } from "@actuallab/fusion-rpc";
import { RpcClientPeer, RpcPeerStateMonitor } from "@actuallab/rpc";
import { defineComputeService } from "@actuallab/fusion-rpc";

// 1. Define your service (must match the .NET interface)
const TodoApiDef = defineComputeService("ITodoApi", {
  Get:        { args: ["", ""] },          // (session, id) → TodoItem
  ListIds:    { args: ["", 0] },           // (session, count) → string[]
  GetSummary: { args: [""] },              // (session) → TodoSummary
  AddOrUpdate: { args: [{}], callTypeId: 0 },  // command (non-compute)
  Remove:      { args: [{}], callTypeId: 0 },  // command (non-compute)
});

// 2. Create hub + peer
const hub = new FusionHub();
const peer = new RpcClientPeer(hub, "ws://localhost:5005/rpc/ws");
hub.addPeer(peer);

// 3. Create typed client proxy
const api = hub.addClient<ITodoApi>(peer, TodoApiDef);

// 4. Start the connection (reconnects automatically)
void peer.run();

// 5. Use the client — compute method results are cached + invalidated automatically
const items = await api.ListIds("~", 10);
```


## Key Differences from .NET

| Aspect | .NET | TypeScript |
|--------|------|------------|
| **Dependency Injection** | `services.AddFusion()` + DI container | Explicit construction: `new FusionHub()`, `hub.addClient(...)` |
| **Compute method marker** | `[ComputeMethod]` attribute + `virtual` method | `@computeMethod` decorator |
| **Invalidation** | `Invalidation.Begin()` block | `boundMethod.invalidate(...args)` |
| **Service interface** | C# interface + proxy generation | `defineComputeService()` or `@rpcService` / `@rpcMethod` decorators |
| **Cancellation** | `CancellationToken` | `AbortSignal` (via `AsyncContext`) |
| **Async context** | `ExecutionContext` / `AsyncLocal<T>` | `AsyncContext` (explicit, thread-local-like) |
| **Serialization** | MemoryPack / MessagePack / System.Text.Json | JSON (`json5np` format, no polymorphism) |
| **UI integration** | `ComputedStateComponent<T>` (Blazor) | `useComputedState` hook (React) |
| **State factory** | `IServiceProvider.StateFactory()` | `new ComputedState(computer, options)` / `new MutableState(initial)` |
| **Streaming** | `RpcStream<T>` + `IAsyncEnumerable<T>` | `RpcStream<T>` + `AsyncIterable<T>` (`for await...of`) |
| **Fire-and-forget** | `Task<RpcNoWait>` return type | `{ returns: RpcType.noWait }` in service definition |


## AsyncContext: Why It Matters

JavaScript is single-threaded but runs asynchronous operations via the event loop.
Unlike .NET's `ExecutionContext` that automatically flows through `await` boundaries,
JavaScript has no built-in async context propagation (the TC39 `AsyncContext` proposal is still Stage 2).

Fusion's `@computeMethod` decorator needs to track which `Computed<T>` is currently being computed,
so it uses `AsyncContext.current` &mdash; a thread-local-like static field.
This works perfectly for synchronous code, but across `await` boundaries
the context can be lost if another microtask runs in between.

**In practice**, you may need to pass `AsyncContext` explicitly as the last argument
when calling compute methods from within other compute methods that cross `await` points:

```ts
class Todos {
  @computeMethod
  async list(count: number, ctx?: AsyncContext): Promise<TodoItem[]> {
    ctx ??= AsyncContext.current;
    const ids = await this.api.ListIds("~", count, ctx);
    // ctx ensures dependency tracking survives the await above
    const items: TodoItem[] = [];
    for (const id of ids) {
      const item = await this.api.Get("~", id, ctx);
      if (item) items.push(item);
    }
    return { items };
  }
}
```

::: tip
`AsyncContext` is only needed when you call compute methods across `await` boundaries
inside other compute methods. If you only call compute methods from React hooks
or non-compute code, you don't need to worry about it.
:::


## Sample App

The [TodoApp TypeScript UI](https://github.com/ActualLab/Fusion.Samples/tree/master/src/TodoApp)
demonstrates a complete React + Fusion setup including:

- Compute service client definition with `defineComputeService`
- Client-side `@computeMethod` that composes server calls
- `useComputedState` for real-time React rendering
- `UIActionTracker` for optimistic UI updates
- `RpcPeerStateMonitor` for connection status UI
- Automatic reconnection with exponential backoff
