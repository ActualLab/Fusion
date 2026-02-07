# TypeScript Fusion Client — Implementation Plan

## Goal

Build a TypeScript client library that allows browser-based (and Node.js) apps
to consume .NET Fusion compute services with the same developer experience:
computed methods, automatic dependency tracking, invalidation, and reactive state.

Implements both server and client for RPC and Fusion compute services in
TypeScript. This enables full TS-only testing and allows the TypeScript
side to host services (e.g., system call callbacks) while also consuming
.NET Fusion services. The final validation step is a .NET integration test
that exposes services and invokes a TypeScript test to query them.

---

## Scope

### In Scope

- **`@actuallab/core`** — foundational primitives ported from .NET
  - `Result<T>` (value-or-error discriminated union)
  - `PromiseSource<T>` (externally-resolvable promise, adapted from ActualChat)
  - `CancellationToken` / `CancellationTokenSource` (lightweight, not AbortController)
  - `Disposable` / `AsyncDisposable` / `DisposableBag`
  - `EventHandlerSet<T>` (typed pub/sub)
  - Module-scoped "ambient context" pattern (replaces .NET AsyncLocal)

- **`@actuallab/rpc`** — JSON-based RPC, both server and client
  - WebSocket transport
  - `json` serialization format (System.Text.Json compatible, non-polymorphic `-np` variant)
  - RPC message envelope: `{CallType, RelatedId, Method, Headers}\n<args>`
  - Argument serialization with `\x1F` (Unit Separator) delimiter
  - `RpcPeer` — both `RpcClientPeer` and `RpcServerPeer`, connection state,
    reconnection, keep-alive. (In .NET's RPC, any peer can expose services —
    system calls like `$sys.Ok` are services exposed by clients that the server
    calls back into. So both sides are needed regardless.)
  - Outbound + inbound call tracking
  - System calls: `Ok`, `Error`, `Cancel`, `Invalidate`, `KeepAlive`
  - `RpcStream<T>` — async iterable for streaming results
  - Handshake protocol
  - Typed service proxy generation (runtime, not codegen) — both client and server

- **`@actuallab/fusion`** — Fusion abstractions, both server and client
  - `Computed<T>` with `ConsistencyState` (Computing / Consistent / Invalidated)
  - `ComputeContext` via `AsyncContext` (explicit last-arg + module-scoped fallback)
  - Automatic dependency tracking during compute method execution
  - `ComputedRegistry` — WeakRef-based global cache of Computed instances
  - Dependency graph: strong refs forward, (ComputedInput, version) backward
  - Invalidation propagation — cascading through local dependency graph
  - `RemoteComputed<T>` — Computed backed by RPC call, waits for server invalidation
  - `ComputedState<T>` — reactive wrapper that auto-updates on invalidation
  - `MutableState<T>` — manually-settable reactive state
  - Compute service definition + proxy — both server-side hosting and client-side proxy
  - Server-side: host compute services, send `$sys-c.Invalidate` to clients
  - Client-side: cache-then-fetch pattern, receive invalidation notifications

### In Scope (limited)

- **Client-side RPC routing** — select a peer for a given call based on
  arguments. No distributed services, no server-side routing. Just a way
  for large clients to route calls to different server endpoints based on
  call arguments (e.g., shard by user ID).

### Out of Scope (for now)

- **Binary RPC protocols** — no MemoryPack, MessagePack, or msgpack6 support
- **Polymorphic type serialization** — the `-np` (non-polymorphic) variant assumes
  both sides know the exact type; no `/* @=TypeName */` prefix handling
- **Distributed services** — no server-side routing or multi-server orchestration
- **Commander / operations framework** — no `ICommand`, `ICommander`,
  `CommandR`, or operation-based pipelines
- **Source generators / build-time codegen** — runtime proxy generation only
- **Blazor integration**
- **EntityFramework / database abstractions**

---

## Architecture

### Package Dependency Chain

```
@actuallab/core  ←  @actuallab/rpc  ←  @actuallab/fusion
```

### Key Design Decisions

#### 1. AsyncContext: General-Purpose Typed Context Container (`@actuallab/core`)

A general-purpose context that carries multiple typed values (ComputeContext,
CancellationToken, etc.). Named `AsyncContext` to align with the TC39 proposal —
if it ever ships, the backing store can change with zero API impact.

**Propagation strategy**: Passed explicitly as the **last argument** to
intercepted methods. If omitted, the interceptor falls back to
`AsyncContext.current` (module-scoped static). If that's also undefined,
uses defaults (e.g., `CancellationToken.none`).

```typescript
export class AsyncContext {
  // Module-scoped ambient — fallback when not passed explicitly
  static current: AsyncContext | undefined = undefined;

  // Typed value storage
  private values = new Map<symbol, unknown>();
  private static defaults = new Map<symbol, unknown>();

  // Get a typed value, falling back to registered default
  get<T>(key: AsyncContextKey<T>): T {
    return (this.values.get(key.id) as T)
      ?? (AsyncContext.defaults.get(key.id) as T)
      ?? key.defaultValue;
  }

  // Set a typed value (returns new AsyncContext — immutable)
  with<T>(key: AsyncContextKey<T>, value: T): AsyncContext {
    const clone = new AsyncContext();
    for (const [k, v] of this.values) clone.values.set(k, v);
    clone.values.set(key.id, value);
    return clone;
  }

  // Make this the current context (returns disposable to restore)
  activate(): Disposable {
    const prev = AsyncContext.current;
    AsyncContext.current = this;
    return { dispose: () => { AsyncContext.current = prev; } };
  }

  // Run fn with this context as current, restore on return
  run<T>(fn: () => T): T {
    const prev = AsyncContext.current;
    AsyncContext.current = this;
    try {
      return fn();
    } finally {
      AsyncContext.current = prev;
    }
  }

  // Register a global default for a key
  static setDefault<T>(key: AsyncContextKey<T>, value: T): void {
    AsyncContext.defaults.set(key.id, value);
  }
}

// Typed key — each subsystem defines its own
export class AsyncContextKey<T> {
  readonly id: symbol;
  readonly defaultValue: T;
  constructor(name: string, defaultValue: T) {
    this.id = Symbol(name);
    this.defaultValue = defaultValue;
  }
}
```

**Subsystems register their keys:**

```typescript
// In @actuallab/core
export const cancellationTokenKey =
  new AsyncContextKey<CancellationToken>("CancellationToken", CancellationToken.none);

// In @actuallab/fusion
export const computeContextKey =
  new AsyncContextKey<ComputeContext | undefined>("ComputeContext", undefined);
```

**Usage from consumer code:**

```typescript
// Implicit — uses AsyncContext.current (set by interceptor/produceComputed)
const product = await productService.getProduct(id);

// Explicit — pass context as last arg
const ctx = new AsyncContext()
  .with(computeContextKey, myComputeCtx)
  .with(cancellationTokenKey, myCt);
const product = await productService.getProduct(id, ctx);

// Just cancellation token
const ctx = new AsyncContext().with(cancellationTokenKey, myCt);
const product = await productService.getProduct(id, ctx);
```

**How the interceptor resolves context:**

```typescript
function resolveContext(
  declaredArgCount: number,
  actualArgs: unknown[],
): AsyncContext {
  // Explicit context as last arg?
  if (actualArgs.length > declaredArgCount) {
    const last = actualArgs[declaredArgCount];
    if (last instanceof AsyncContext) return last;
  }
  // Fall back to ambient
  return AsyncContext.current ?? new AsyncContext();
}
```

**Across `await` boundaries**: Dependency capture in Fusion happens
synchronously at the Proxy intercept point (before any `await`). For
sequential `await`s within a compute method body, the Proxy wraps the
returned promise to re-set `AsyncContext.current` from the captured
context before the caller's continuation runs. For concurrent independent
computations (multiple `ComputedState` updates), explicit context passing
or serialized updates are required.

#### 2. CancellationToken — Lightweight, Not AbortController

AbortController is DOM API with overhead and different semantics. We implement
a minimal `CancellationToken` / `CancellationTokenSource` similar to .NET:

```typescript
export class CancellationTokenSource implements Disposable {
  readonly token: CancellationToken;
  cancel(): void;
  dispose(): void;
}

export class CancellationToken {
  static readonly none: CancellationToken;
  static readonly cancelled: CancellationToken;
  readonly isCancelled: boolean;
  throwIfCancelled(): void;
  onCancelled(callback: () => void): Disposable;
  // Interop: toAbortSignal() for fetch/DOM APIs
  toAbortSignal(): AbortSignal;
}
```

CancellationToken is pre-registered as a default in AsyncContext:
```typescript
AsyncContext.setDefault(cancellationTokenKey, CancellationToken.none);
```

#### 3. Computed<T> — Dependency Graph & Registry

**Dependency graph — asymmetric references:**

If compute method A calls compute method B, then A depends on B:

```
A's Computed                          B's Computed
┌──────────────────┐                  ┌──────────────────┐
│ _dependencies:   │──── strong ────→ │                  │
│   Set<Computed>  │     reference    │ _dependants:     │
│                  │                  │   Set<(Input,    │
│                  │ ←── weak-like ── │     version)>    │
└──────────────────┘     (lookup)     └──────────────────┘
```

- **`_dependencies`** (forward: A → B): **strong references** (`Set<Computed>`).
  Keeps B alive as long as A is alive. Guarantees B's invalidation always
  reaches A — B cannot be GC'd while A exists.

- **`_dependants`** (backward: B → A): **`(ComputedInput, version)` pairs**.
  NOT a direct reference to A. To notify A, resolve `ComputedInput` via
  `ComputedRegistry` → get `Computed`, check `version` matches. If A was
  evicted or replaced by a newer version → skip (dead reference).

This asymmetry is key: strong refs forward guarantee invalidation correctness,
weak-like refs backward avoid preventing GC of unused dependants.

**On invalidation of B:**
```typescript
for (const [input, version] of this._dependants) {
  const dependant = ComputedRegistry.get(input);
  if (dependant != null && dependant.version === version)
    dependant.invalidate();
  // else: dependant was GC'd or superseded — skip
}
this._dependants.clear();
```

**ComputedRegistry** — global cache of Computed instances:

JavaScript's `WeakRef` (ES2021, ~97% browser support) and `FinalizationRegistry`
allow GC-aware caching similar to .NET's `WeakReference<T>`:

```typescript
// ComputedRegistry stores weak refs, keyed by ComputedInput
const registry = new Map<string, WeakRef<Computed<any>>>();
const _gr = new FinalizationRegistry<string>(key => registry.delete(key));
```

Note: the registry uses `WeakRef` so that Computed instances with no
strong references (no one depends on them, no `ComputedState` holds them)
can be GC'd. The strong references in `_dependencies` keep the dependency
chain alive as needed.

#### 4. RPC Wire Format — JSON Non-Polymorphic

We implement a simplified JSON protocol (`json-np`) that:
- Uses the same envelope: `{"CallType":N,"RelatedId":N,"Method":"..."}\n<args>`
- Delimits arguments with `\x1F` (Unit Separator)
- Does **not** include `/* @=TypeName */` type prefixes
- Serializes/deserializes with standard `JSON.stringify` / `JSON.parse`
- Sends as WebSocket text frames

This requires the .NET server to support this format variant (or we use `json5`
with the server configured to not require type decorators for the TS client's
service contracts).

#### 5. Method Interception & Service Definitions

We need the TypeScript equivalent of `RpcServiceDef` / `RpcMethodDef` to know:
- Which methods exist on a service
- How many arguments each method has (needed for `\x1F`-delimited serialization)
- Whether a method is a compute method (affects caching/invalidation behavior)
- Whether a method returns a stream

This metadata is declared explicitly at service definition time (no reflection):

```typescript
// RpcType descriptors — used for arg types and return types
export const RpcType = {
  object:  Symbol("object"),   // JSON-serializable value (default)
  stream:  Symbol("stream"),   // RpcStream<T>
  void:    Symbol("void"),     // no return value
  // Extensible: RpcType.blob, etc.
} as const;

// Service definition — declares the contract
const IProductService = defineComputeService("ProductService", {
  getProduct:  { args: [""],  returns: RpcType.object, compute: true },
  getProducts: { args: ["", 0], returns: RpcType.stream, compute: true },
  editProduct: { args: [{}],  returns: RpcType.void },
});
```

**`args`**: Array of type descriptors or **example values** that imply the type.
- `""` or `"x"` → string arg
- `0` or `1` → number arg
- `{}` → object arg
- `new RpcStream()` → stream arg
- `RpcType.object` → explicit type descriptor
- Array length = argument count (derived automatically)

**`returns`**: `RpcType.object` (default), `RpcType.stream`, `RpcType.void`,
or an example value.

**`compute`**: `true` if this is a compute method (affects caching/invalidation).

A `Proxy` intercepts method calls, looks up the method definition to determine
argument count, return type, serialization behavior, and whether to go through
the compute pipeline or plain RPC:

```typescript
// Creates a typed client from the definition
const productService = createClient<IProductService>(rpcHub, IProductService);

// Usage — identical feel to .NET
const product = await productService.getProduct(id);
```

**Interception flow for compute methods:**
1. Proxy trap intercepts `productService.getProduct(id)`
2. Looks up method def → `{ args: [""], returns: RpcType.object, compute: true }`
3. Extracts arg count from `args.length` (1 in this case)
4. Routes through `RemoteComputeMethodFunction` → checks `ComputedRegistry`
5. If cached & consistent → return cached value
6. Otherwise → send RPC call, create `RemoteComputed<T>`, register for invalidation

**Interception flow for plain RPC methods:**
1. Proxy trap intercepts `productService.editProduct(command)`
2. Looks up method def → `{ args: [{}], returns: RpcType.void }` (no `compute` flag)
3. Sends a plain `RpcOutboundCall`, awaits `Ok` / `Error`

#### 6. Context Passing Convention — AsyncContext as Last Argument

The **last argument** to any intercepted method (beyond the declared arg
count) may optionally be an `AsyncContext`. The Proxy uses the method
definition's `args` count to distinguish positional args from the context:

```typescript
// All of these are valid calls:
await productService.getProduct(id);          // uses AsyncContext.current
await productService.getProduct(id, ctx);     // explicit AsyncContext
```

The interceptor extracts typed values from the resolved context:

```typescript
function handleComputeMethod(methodDef: MethodDef, actualArgs: unknown[]) {
  const ctx = resolveContext(methodDef.argCount, actualArgs);
  const computeCtx = ctx.get(computeContextKey);   // ComputeContext | undefined
  const ct = ctx.get(cancellationTokenKey);          // CancellationToken (defaults to .none)
  // ...
}
```

This keeps the public API clean (one optional last arg) while supporting
arbitrary context values. New subsystems can add their own keys without
changing the method signatures.

---

## Package Breakdown

### `@actuallab/core`

| Module | Types | Notes |
|--------|-------|-------|
| `async-context.ts` | `AsyncContext`, `AsyncContextKey<T>` | General-purpose typed context container, `current` static, `run()`, `activate()`, `get()`, `with()` |
| `result.ts` | `Result<T>`, `ResultOk<T>`, `ResultError<T>` | Discriminated union with `isOk()` / `isError()` type guards |
| `promise-source.ts` | `PromiseSource<T>` | Adapted from ActualChat's implementation |
| `cancellation.ts` | `CancellationToken`, `CancellationTokenSource`, `cancellationTokenKey` | Lightweight, with `toAbortSignal()` interop, registered as AsyncContext default |
| `disposable.ts` | `Disposable`, `AsyncDisposable`, `DisposableBag` | `.dispose()` pattern, adapted from ActualChat |
| `events.ts` | `EventHandlerSet<T>` | `add()`, `remove()`, `trigger()`, `whenNext()` |
| `async-lock.ts` | `AsyncLock` | Promise-based mutual exclusion |
| `version.ts` | `nextVersion()` | Global monotonic version counter |

### `@actuallab/rpc`

| Module | Types | Notes |
|--------|-------|-------|
| `rpc-message.ts` | `RpcMessage`, `RpcCallTypeId` | Envelope structure, system method IDs |
| `rpc-serialization.ts` | `serializeMessage()`, `deserializeMessage()` | JSON envelope + `\x1F`-delimited args |
| `rpc-peer.ts` | `RpcPeer`, `RpcClientPeer`, `RpcServerPeer` | Connection lifecycle, reconnection with backoff, bidirectional service hosting |
| `rpc-connection.ts` | `RpcConnection` | WebSocket wrapper, frame reading/writing |
| `rpc-outbound-call.ts` | `RpcOutboundCall` | Tracks pending call, resolves on `Ok`/`Error` |
| `rpc-call-tracker.ts` | `RpcOutboundCallTracker`, `RpcInboundCallTracker` | Map of RelatedId → call, bidirectional tracking |
| `rpc-system-calls.ts` | `handleSystemCall()` | Dispatches `Ok`, `Error`, `Cancel`, `Invalidate` |
| `rpc-stream.ts` | `RpcStream<T>` | AsyncIterable with batching and ACK |
| `rpc-inbound-call.ts` | `RpcInboundCall` | Tracks incoming call, sends Ok/Error response |
| `rpc-service-host.ts` | `RpcServiceHost` | Dispatches inbound calls to registered service implementations |
| `rpc-client.ts` | `createClient<T>()` | Proxy-based typed service client factory |
| `rpc-hub.ts` | `RpcHub` | Central coordinator, peer management, service registry |

### `@actuallab/fusion`

| Module | Types | Notes |
|--------|-------|-------|
| `computed.ts` | `Computed<T>`, `ConsistencyState` | Core cached computation with dependency tracking |
| `compute-context.ts` | `ComputeContext`, `CallOptions` | Module-scoped ambient context |
| `computed-input.ts` | `ComputedInput` | Method + args identity key for registry lookup |
| `computed-registry.ts` | `ComputedRegistry` | WeakRef-based global Computed cache |
| `invalidation.ts` | `Invalidation` | Scope-based invalidation + cascade propagation |
| `remote-computed.ts` | `RemoteComputed<T>` | Computed backed by RPC, bound to `WhenInvalidated` |
| `computed-state.ts` | `ComputedState<T>` | Auto-updating reactive state wrapper |
| `mutable-state.ts` | `MutableState<T>` | Manually-settable reactive state |
| `compute-service.ts` | `defineComputeService()` | Service definition + client proxy creation |
| `compute-service-host.ts` | `ComputeServiceHost` | Server-side: hosts compute services, tracks active compute calls, sends `$sys-c.Invalidate` on invalidation |

---

## Design Principles

1. **Mirror Fusion's public API** — types, method names, and behavior should
   closely match the .NET version. The main deltas are:
   - TypeScript naming conventions (camelCase methods, PascalCase types)
   - No interfaces where a single class suffices (simplify where .NET uses
     `IComputed` + `Computed` + `Computed<T>` hierarchy)
   - No multi-threading support (no locks, volatile, Interlocked)
   - `Promise<T>` replaces `Task<T>` / `ValueTask<T>`

2. **If Fusion has an abstraction, we need a TS equivalent** — don't skip
   things like update delays, computed options, etc. Port them, simplified.

3. **No dependency injection** — Fusion setup is a set of singletons.
   For RPC, `RpcHub` is the root object (can have multiple instances),
   and `rpcHub.services` is a `Map<string, RpcServiceDef>`.

4. **No codegen** — all interception is runtime (JS Proxy + metadata objects).
   Service contracts are defined as plain object literals.

---

## Implementation Phases

### Phase 1: Core Primitives (`@actuallab/core`)

Foundational types that everything else depends on.

1. `AsyncContext` / `AsyncContextKey<T>` — typed context container with
   `current` static, `get()`, `with()`, `run()`, `activate()`
2. `Result<T>` — discriminated union (`ok(value)` / `error(err)`)
3. `PromiseSource<T>` — externally-resolvable promise (adapted from ActualChat)
4. `CancellationToken` / `CancellationTokenSource` + `cancellationTokenKey`
   registered as AsyncContext default
5. `Disposable` / `AsyncDisposable` / `DisposableBag`
6. `EventHandlerSet<T>` — typed pub/sub
7. `AsyncLock` — promise-based mutual exclusion
8. `nextVersion()` — global monotonic version counter

**Exit criteria**: All types have tests, `npm run test` passes.

### Phase 2: Fusion Abstractions (`@actuallab/fusion`) — Local-Only

The core of Fusion: computed values, dependency tracking, invalidation, state.
All tested locally (no RPC yet) using mock/manual invalidation.

#### 2a. Computed & Registry

1. `ComputeContext` — module-scoped `current`, `CallOptions` flags,
   `run(ctx, fn)` save/restore pattern
2. `Computed<T>` — consistency states (`Computing` / `Consistent` / `Invalidated`),
   version, `Result<T>` output, dependency/dependant tracking,
   `invalidate()` with cascading, `use()` / `value` accessors
3. `ComputedInput` — identity key (function ref + serialized args),
   equality, hash
4. `ComputedRegistry` — `WeakRef`-based global cache, `get(input)`,
   `register(input, computed)`, `FinalizationRegistry` for auto-cleanup

#### 2b. Compute Functions & Interception

5. `ComputeFunction<T>` — produces `Computed<T>` from `ComputedInput`,
   handles cache lookup, locking (one computation per input at a time),
   calling the actual function body, capturing dependencies
6. `Interceptor` base — `selectHandler(invocation)` → handler or null,
   handler cache per method, null-coalescing chain
7. `Invocation` — `{ proxy, methodDef, args, interceptedFn }`
8. `ComputeServiceInterceptor` — intercepts compute methods,
   routes through `ComputeFunction`
9. Service definition + Proxy-based client wiring (local compute services
   for testing dependency tracking end-to-end)

#### 2c. State & Update Delays

10. `UpdateDelayer` — controls when state re-computation happens after
    invalidation. Implementations: `FixedDelayer(ms)`, `NoDelayer`
11. `ComputedState<T>` — wraps a compute function, auto-updates on
    invalidation with configurable delay, fires `invalidated` / `updated`
    events, tracks `lastNonErrorValue`
12. `MutableState<T>` — manually-settable state, fires change events,
    can be used as a dependency by computed methods

**Exit criteria**: Can define local compute functions, build a dependency
graph, invalidate a leaf, see cascading invalidation + state auto-update.
All tested without any RPC.

### Phase 3: RPC Layer (`@actuallab/rpc`) — Both Server & Client

Wire-compatible RPC that speaks the Fusion `json5` protocol. Both client
peer (connects to server) and server peer (accepts connections, dispatches
inbound calls) are implemented. This is necessary because:
- System calls (`$sys.Ok`, `$sys.Error`, etc.) are services exposed by the
  *client* that the server calls back into — so both directions are needed.
- TS-only tests need a TS server peer to test against.

#### 3a. Core RPC Abstractions

1. `RpcMessage` — envelope: `{ callType, relatedId, method, headers }`
2. `RpcSerializer` — JSON envelope + `\x1F`-delimited args serialization,
   `\x0A` envelope/args delimiter, `\x0A\x1E` frame delimiter
3. `RpcMethodDef` — method name, arg count, compute flag, stream flag
4. `RpcServiceDef` — service name, methods map, compute options
5. `RpcPeerRef` — peer identity (key for peer lookup)
6. `RpcCallTypeId` — enum: Regular, Ok, Error, Cancel, etc.
7. `RpcSystemMethodKind` — system call identifiers

#### 3b. Connection & Peer

8. `RpcConnection` — WebSocket wrapper, send/receive frames,
   text message parsing (split on `\x0A\x1E`)
9. `RpcPeer` — base class: connection lifecycle, send/receive,
   call tracking, keep-alive
10. `RpcClientPeer` extends `RpcPeer` — initiates connection,
    handshake (`$sys.Handshake`), reconnection (`$sys.Reconnect`),
    exponential backoff
11. `RpcServerPeer` extends `RpcPeer` — accepts connection,
    responds to handshake, dispatches inbound calls to hosted services
12. `RpcHub` — central coordinator, peer management, `services: Map`,
    no DI — just a plain object holding config + peers + services

#### 3c. Call Tracking & System Calls (bidirectional)

13. `RpcOutboundCall` — tracks pending outbound call, `PromiseSource`
    for result, timeout management
14. `RpcOutboundCallTracker` — `Map<relatedId, RpcOutboundCall>`
15. `RpcInboundCall` — tracks incoming call, dispatches to service
    implementation, sends `$sys.Ok` / `$sys.Error` response
16. `RpcInboundCallTracker` — `Map<relatedId, RpcInboundCall>`
17. System call handling (both directions): `$sys.Ok`, `$sys.Error`,
    `$sys.Cancel`, `$sys.KeepAlive` (15s send interval, 55s timeout)
18. `RpcOutboundComputeCall` extends `RpcOutboundCall` —
    adds `whenInvalidated: PromiseSource`, linked to `$sys-c.Invalidate`

#### 3d. Service Proxy & Service Host

19. `defineRpcService()` / `defineComputeService()` — metadata declaration
20. `createClient<T>(rpcHub, serviceDef)` — JS `Proxy`-based typed client,
    intercepts method calls, looks up `RpcMethodDef`, serializes args,
    sends `RpcOutboundCall`, returns `Promise<T>`
21. `RpcServiceHost` — dispatches inbound calls to registered service
    implementations. Looks up `RpcServiceDef` by method name, invokes
    the implementation function, sends result back via `$sys.Ok` / `$sys.Error`
22. `RpcStream<T>` — `AsyncIterable<T>` backed by `$sys.I` / `$sys.B`
    item messages, ACK protocol

**Exit criteria**: TS-only test — a TS RPC server hosts a plain service,
a TS RPC client connects, calls a method, gets a result. Keep-alive works.
Also: can connect to a .NET server and call a plain RPC method.

### Phase 4: Fusion + RPC Integration (`@actuallab/fusion`) — Both Server & Client

Connect the Fusion abstractions from Phase 2 to the RPC layer from Phase 3,
implementing both the compute service host (server) and compute service
client. This enables fully TS-only testing of the Fusion compute pipeline.

#### 4a. Server-Side: Compute Service Hosting

1. `ComputeServiceHost` — hosts compute service implementations behind
   `RpcServiceHost`. When a compute method is called:
   - Runs the compute function (Phase 2 `ComputeFunction`)
   - Returns the result via `$sys.Ok`
   - Watches the resulting `Computed<T>` for invalidation
   - Sends `$sys-c.Invalidate` to the calling peer when the computed
     value is invalidated (RelatedId matches the original call)
2. `RpcInboundComputeCall` extends `RpcInboundCall` — tracks the
   `Computed<T>` produced by the call, subscribes to its invalidation,
   stays alive until client disconnects or cancels

#### 4b. Client-Side: Remote Compute Service

3. `RemoteComputed<T>` — `Computed<T>` backed by `RpcOutboundComputeCall`,
   bound to `whenInvalidated`, syncs with server
4. `RemoteComputeMethodFunction` — cache-then-fetch pattern:
   return cached value immediately, background RPC update
5. `RemoteComputeServiceInterceptor` — chains compute interceptor + RPC
   interceptor via `selectHandler() ?? rpcInterceptor.selectHandler()`
6. Integration of `ComputedState<T>` with remote computed values —
   auto-update loop that re-fetches from server on invalidation

#### 4c. Synchronization

7. `ComputedSynchronizer` — tracks whether local computed + all its
   remote dependencies are synchronized with the server

**Exit criteria**: Full TS-only end-to-end test:
- TS server hosts a compute service
- TS client calls a compute method, gets cached `Computed<T>`
- Server-side invalidation → `$sys-c.Invalidate` sent → client receives
  invalidation → cascading invalidation + `ComputedState` auto-update

### Phase 5: Polish & Hardening

1. Client-side `RemoteComputedCache` (optional, e.g. IndexedDB)
2. Connection state events (connected / disconnected / reconnecting)
3. Error handling, retry policies, call timeouts
4. `ComputedGraphPruner` — periodic cleanup of unreachable computed
5. Logging infrastructure
6. Documentation and examples

### Phase 6: .NET Integration Test

The ultimate validation — prove wire compatibility with .NET Fusion.

1. **.NET test project**: Create a test that programmatically starts
   an ASP.NET host exposing both a plain RPC service and a Fusion
   compute service via WebSocket RPC (`json5` format)
2. **TypeScript test**: Invoked from the .NET test (e.g., via
   `node --test` or `vitest run`), connects to the .NET server:
   - Calls a plain RPC method, verifies the result
   - Calls a compute method, gets `Computed<T>`, verifies the value
   - Triggers server-side invalidation (via a mutation RPC call)
   - Verifies the client receives `$sys-c.Invalidate` and the
     `ComputedState` auto-updates with the new value
3. **CI integration**: The .NET test orchestrates the full lifecycle
   (start server → run TS test → assert → shutdown)

**Exit criteria**: `dotnet test` runs the .NET host + TS client
end-to-end, all assertions pass, proving wire compatibility.

---

## Open Questions — Research Required

These questions must be answered before implementation begins. Each needs
research into both the .NET Fusion codebase and TypeScript/JavaScript ecosystem.

### Q1. Interception & Interceptor Chain Architecture

**Context**: In .NET Fusion, method interception is based on code generation
(source generators + Castle.Core/custom proxies). An `Invocation` object flows
through a chain of `IInterceptor` implementations. The chain includes:
- `ComputeServiceInterceptor` (handles compute method caching)
- `RpcInterceptor` (handles RPC calls)
- `RemoteComputeServiceInterceptor` (combines both for remote services)

**Question**: What interception model should the TypeScript version use?

**Options to evaluate**:
- **(A) Generic interceptor chain** — Port the `Invocation` + `IInterceptor[]`
  pattern. Decorators or Proxy wraps the method, builds an Invocation, passes
  it through a configurable chain. Most flexible, closest to .NET.
- **(B) Purpose-built Proxy per concern** — No generic chain. The Proxy handler
  directly implements compute-then-RPC logic in a single function. Simpler,
  less abstraction, but harder to extend.
- **(C) TypeScript decorators** — Use TC39 stage 3 decorators to annotate
  methods on a class. Each decorator adds behavior (compute caching, RPC).
  Requires class-based service definitions.
- **(D) Hybrid** — Generic interceptor chain internally, but exposed via
  a simple `defineComputeService()` API that hides the chain from users.

**Research needed**: How exactly does Fusion's `Invocation`, `Interceptor`,
`ComputeServiceInterceptor`, and `RpcInterceptor` work? What does the
interceptor chain look like for a remote compute method call?

### Q2. Ambient Context (ComputeContext) Propagation

**Context**: .NET uses `AsyncLocal<ComputeContext?>` to propagate the current
computation context through async call chains. JavaScript has no equivalent
with cross-browser support.

**Question**: How to propagate ComputeContext through compute method calls?

**Options to evaluate**:
- **(A) Module-scoped static variable only** — `ComputeContext.current` is a
  module-level `let`. Set/restore in try/finally around compute method body.
  Works because JS is single-threaded and dependency capture is synchronous.
  Breaks if user code does concurrent `Promise.all()` of compute methods.
- **(B) Explicit last-argument passing** — The last argument (beyond declared
  arg count) is the context. Falls back to static variable if omitted.
  Handles `Promise.all()` correctly since each call carries its own context.
- **(C) Combined A+B** — Static variable for the simple/common case,
  explicit passing for advanced cases (concurrent compute calls).
  The proxy always reads the static, but the user can override per-call.
- **(D) Zone.js / AsyncContext polyfill** — Use Zone.js or
  `@webfill/async-context` for real async propagation. Heavy dependency
  (~100KB for Zone.js), invasive monkey-patching.

**Research needed**: Verify that dependency capture in Fusion is indeed
synchronous (happens at the point of calling a compute method, before any
await). If so, option A/C works for most cases.

### Q3. RPC Wire Protocol Compatibility

**Question**: Can we use the existing `json5` format as-is, or do we need
a new `json-np` variant on the .NET side?

**Sub-questions**:
- What happens when the .NET server serializes a response with polymorphic
  type decorators (`/* @=TypeName */`) and the TS client doesn't expect them?
- Can the server be configured per-peer to skip type decorators?
- What is the exact handshake protocol? (Hub ID, format negotiation, version)
- How are system calls structured on the wire? (`$sys.Ok`, `$sys.Error`,
  `$sys.Invalidate`, etc.)

**Research needed**: Read the .NET handshake code, system call serialization,
and type decorator logic to determine if we need server-side changes.

### Q4. Service & Method Definition Metadata

**Context**: .NET uses reflection + attributes (`[ComputeMethod]`,
`[RpcMethod]`) to discover service methods, argument counts, and
configuration. TypeScript has no runtime reflection over interfaces.

**Question**: How should service contracts be defined in TypeScript?

**Options to evaluate**:
- **(A) Plain object literal** — `{ getProduct: { args: 1, compute: true } }`.
  Simple, no decorators, works with any module system.
- **(B) Decorator-based class** — Define a class with decorated methods.
  Requires `experimentalDecorators` or TC39 decorators. More familiar to
  Angular/NestJS developers.
- **(C) Schema/codegen from .NET** — Generate TypeScript service definitions
  from the .NET server's RPC service registry. Most accurate, but adds a
  build step.
- **(D) Hybrid** — Object literal for the core definition, with an optional
  codegen tool that produces these definitions from .NET metadata.

**Research needed**: How does .NET's `RpcServiceDef` / `RpcMethodDef` work?
What metadata is essential (arg count, method name mapping, compute options)?

### Q5. Dependency Capture Mechanics

**Context**: In .NET Fusion, when compute method A calls compute method B,
B's `Computed<T>` is automatically registered as a dependency of A's
`Computed<T>`. This happens via `ComputeContext.Current` — the currently
executing computation's context captures each accessed Computed.

**Question**: How exactly should dependency capture work in the TS client,
given that all compute methods are remote?

**Sub-questions**:
- In the client-only scenario, are there local compute methods that depend
  on other local compute methods? Or is every compute method a remote call?
- If all methods are remote, does the dependency graph exist only on the
  server? Does the client only need to track "which RemoteComputed instances
  am I currently using" rather than a full dependency graph?
- Do we need local-only compute methods (pure client-side computed values
  that depend on remote computed values)?

**Research needed**: Understand the .NET client-side dependency tracking
in `RemoteComputeServiceInterceptor`. Does the .NET client build its own
local dependency graph, or does it rely entirely on server-side invalidation?

### Q6. Keep-Alive, Heartbeat & Reconnection Protocol

**Question**: What are the exact keep-alive/heartbeat requirements?

**Research needed**: Read `RpcClientPeer`, `RpcServerPeer` for ping/pong
or keep-alive message patterns. What happens when a client reconnects —
does it need to re-register for invalidation notifications?

---

## Research Findings

### F1. Interception & Interceptor Chain (answers Q1)

**.NET Architecture**:
- `Invocation` is a readonly struct: `{ proxy, method: MethodInfo, arguments: ArgumentList, interceptedDelegate: Delegate }`
- `Interceptor` base class has `SelectHandler(invocation) → Func<Invocation, object?> | null`
- Handlers are **cached per MethodInfo** in a `ConcurrentDictionary` for performance
- Chain is composed via **null-coalescing override**:
  ```csharp
  // RemoteComputeServiceInterceptor
  SelectHandler(inv) => GetHandler(inv) ?? RpcInterceptor.SelectHandler(inv)
  ```
- `ArgumentList` has generic arity variants (`ArgumentListG1<T0>` through `ArgumentListG10<>`)
  with `Get<T>(index)`, `Set<T>(index, value)`, `GetCancellationToken(index)`
- Proxy classes are **source-generated** — each method creates an `Invocation`,
  calls `Interceptor.Intercept<TResult>(invocation)`, which calls `SelectHandler`
  to get the handler, then invokes it

**Recommended option: (D) Hybrid — generic interceptor chain, simple user API**

The interceptor chain is elegant but the TypeScript version doesn't need the full
generality. Since we're client-only with a single interception path (compute-then-RPC),
the chain can be simplified:

```typescript
// Internal: interceptor chain (for extensibility)
type Handler = (invocation: Invocation) => unknown;
type HandlerFactory = (methodDef: MethodDef) => Handler | null;

class Interceptor {
  private cache = new Map<string, Handler | null>();
  selectHandler(invocation: Invocation): Handler | null { ... }
}

// User-facing: simple service definition
const IProductService = defineComputeService("ProductService", {
  getProduct: { args: 1, compute: true },
});
```

The chain is hidden from users. `defineComputeService` wires up the
`RemoteComputeServiceInterceptor` internally. Advanced users could
plug in custom interceptors if needed.

**ArgumentList**: In TypeScript, use a plain `unknown[]` array. No need for
generic arity variants — TypeScript's type system handles this at compile time.
The method definition's `args` count provides the runtime metadata.

---

### F2. Context Propagation (answers Q2)

**Critical finding**: Dependency capture in .NET Fusion is **synchronous**.

The call chain is:
1. Proxy intercept → reads `ComputeContext.Current` synchronously
2. `ComputedInput.GetOrProduceComputed(context, ct)` — context passed as local var
3. `ComputedImpl.TryUseExisting(existing, context)` → `context.Computed?.AddDependency(existing)`
4. Dependency registered **before any await**

This means the module-scoped variable approach is safe even with `Promise.all`:

```typescript
// Both Proxy intercepts run synchronously before await yields
const [a, b] = await Promise.all([
  service.computeA(),  // Proxy reads ComputeContext.current synchronously
  service.computeB(),  // Proxy reads ComputeContext.current synchronously
]);
// By this point, both dependencies are already captured
```

Each Proxy intercept completes its synchronous work (read context, check cache,
register dependency) before JS yields at `await Promise.all(...)`.

For cache misses (inner compute method body runs), `BeginCompute` sets a new
context via save/restore:
```typescript
const prev = ComputeContext.current;
ComputeContext.current = new ComputeContext(newComputed);
try {
  const promise = actualBody(...args); // sync portion captures deps
  return promise;
} finally {
  ComputeContext.current = prev; // restore before yielding
}
```

**Final decision: `AsyncContext` — explicit last-arg passing + module-scoped fallback**

- `AsyncContext` is a general-purpose typed context container in `@actuallab/core`
- Primary propagation: **explicit last argument** to intercepted methods
- Fallback: `AsyncContext.current` module-scoped static (set by `run()` / `activate()`)
- Typed values via `ctx.get(key)`: `ComputeContext`, `CancellationToken`, extensible
- Dependency capture happens synchronously at the Proxy intercept point — the
  interceptor reads context once and passes it as a local variable from there
- For sequential `await`s within a compute body: the Proxy wraps returned
  promises to re-set `AsyncContext.current` from the captured context
- For concurrent independent computations: explicit context passing required
- Named `AsyncContext` for forward-compatibility with TC39 proposal — if it
  ships, the backing store can change with zero API impact

---

### F3. RPC Wire Protocol (answers Q3)

**Handshake**: Both sides send `$sys.Handshake` as fire-and-forget:
```
{"Method":"$sys.Handshake"}\n
{"RemotePeerId":"<guid>","RemoteApiVersionSet":null,"RemoteHubId":"<guid>","ProtocolVersion":2,"Index":0}
```

**System calls** (service `$sys`):
| Method | Wire name | Args | Notes |
|--------|-----------|------|-------|
| Ok | `$sys.Ok` | `result` (any) | RelatedId matches outbound call |
| Error | `$sys.Error` | `ExceptionInfo` | RelatedId matches outbound call |
| Cancel | `$sys.Cancel` | *(none)* | RelatedId identifies call to cancel |
| KeepAlive | `$sys.KeepAlive` | `long[]` localIds | Sent every 15s |
| Reconnect | `$sys.Reconnect` | `handshakeIndex, completedStagesData` | After disconnect |

**Compute system calls** (service `$sys-c`):
| Method | Wire name | Args | Notes |
|--------|-----------|------|-------|
| Invalidate | `$sys-c.Invalidate` | *(none)* | RelatedId identifies which outbound compute call is invalidated |

**Frame delimiters** (WebSocket text mode):
- Messages within a frame separated by `\x0A\x1E` (LF + Record Separator)
- Envelope and arguments separated by `\x0A` (LF)
- Arguments separated by `\x1F` (Unit Separator)

**Polymorphism**: Type decorator `/* @=TypeName */` is ONLY added when:
- Declared parameter type is `abstract` OR `object`
- `IsPolymorphic(type) => type.IsAbstract || type == typeof(object)`

**Recommended approach**: Use `json5` as-is. If service contracts use concrete
types (which they should for a TS client), no type decorators are emitted.
A `-np` variant may still be useful as a safety net to guarantee no type
prefixes are ever sent, but it's not strictly required. The TS deserializer
should be able to strip `/* @=... */` prefixes as a fallback.

**Keep-alive**: Client must send `$sys.KeepAlive` every 15 seconds with IDs
of active shared objects. Server disconnects after 55s of silence.

**Reconnection**: Client sends new `$sys.Handshake` with incremented `Index`,
then `$sys.Reconnect` with previous handshake index + compressed completed
call IDs. Server responds with unresolved call IDs to resend.

---

### F4. Service & Method Definition Metadata (answers Q4)

**Essential metadata per method** (from .NET `RpcMethodDef` / `ComputeMethodDef`):
- Method name (string, maps to `ServiceName.MethodName` on the wire)
- Parameter count (needed for argument serialization)
- CancellationToken parameter index (or -1 if none)
- Whether it's a compute method (boolean)
- Whether it returns a stream (boolean)
- ComputedOptions: `minCacheDuration`, `autoInvalidationDelay`, etc.

**Recommended option: (A) Plain object literal** with optional (D) codegen:

```typescript
const IProductService = defineComputeService("ProductService", {
  getProduct:  { args: 1, compute: true },
  getProducts: { args: 2, compute: true, stream: true },
  editProduct: { args: 1 },
});
```

This is the minimum viable definition. A future codegen tool could emit these
from the .NET server's RPC metadata for accuracy.

---

### F5. Dependency Capture Mechanics (answers Q5)

**Critical finding**: The .NET client DOES maintain a full local dependency graph.

- `Computed` instances on the client have both `_dependencies` and `_dependants` sets
- Local compute methods CAN depend on `RemoteComputed<T>` instances
- When `RemoteComputed<T>` is invalidated by the server (via `$sys-c.Invalidate`),
  it cascades invalidation to all local dependants through the dependency graph
- `ComputedSynchronizer` recursively checks whether all dependencies (including
  remote) are synchronized before considering a local computed "synchronized"

**For the TS port**: We need the full dependency graph on the client side.
This means:
1. Each `Computed<T>` tracks its dependencies and dependants
2. `RemoteComputed<T>` receives server invalidation and cascades locally
3. Local compute methods (client-side only) can depend on remote ones
4. The dependency graph enables "local computed derived from remote data" —
   e.g., a client-side computed that combines two remote values

---

### F6. Keep-Alive & Reconnection (answers Q6)

**Keep-alive**:
- Send `$sys.KeepAlive` with `long[]` of active shared object IDs every 15 seconds
- Server disconnects if no keep-alive received for 55 seconds
- Call timeout logging at 30 seconds (warning, not disconnect)

**Reconnection**:
1. Establish new WebSocket connection
2. Send `$sys.Handshake` with incremented `Index` and same `RemoteHubId`
3. Send `$sys.Reconnect(previousHandshakeIndex, completedStagesData)`
   - `completedStagesData`: `Map<stage, compressedCallIds>` using VarInt delta encoding
4. Server returns byte array of unresolved call IDs → client resends those
5. All active `RpcOutboundComputeCall` instances re-register for invalidation

---

## Decision Summary

| Question | Recommended Option | Rationale |
|----------|-------------------|-----------|
| Q1. Interception | **(D) Hybrid** — generic chain, simple API | Extensible internally, clean user-facing `defineComputeService()` |
| Q2. Context | **AsyncContext: explicit last-arg + static fallback** | Explicit passing handles concurrent case; static fallback for convenience; forward-compatible with TC39 |
| Q3. Protocol | **Use `json5` as-is** | Polymorphism only applies to abstract/object types; concrete contracts are clean |
| Q4. Metadata | **(A) Object literal** + future codegen | Simple, no decorators needed, optional codegen later |
| Q5. Dependencies | **Full local graph** | Client needs local dependency tracking for cascading invalidation |
| Q6. Keep-alive | **15s interval, 55s timeout** | Must match .NET server expectations exactly |
