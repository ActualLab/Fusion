# Review partitions (round 2)

Each reviewer owns exactly one partition. Read **only** your partition deeply,
but you may read anything else in the repo as *context* (callers, base classes,
tests, docs) when you need it to prove or disprove a finding. Report findings
that live inside your partition. If you find something clearly severe outside
your partition, report it in a trailing `## Out-of-partition findings` section.

---

## P1 — RPC transport, peers & connection lifecycle

Scope:
- `src/ActualLab.Rpc/WebSockets/`
- `src/ActualLab.Rpc/Clients/`
- `src/ActualLab.Rpc/Configuration/`
- `src/ActualLab.Rpc/*.cs` (project root files)
- The peer/connection part of `src/ActualLab.Rpc/Infrastructure/`:
  `RpcPeer*`, `RpcClientPeer*`, `RpcServerPeer*`, `RpcConnection*`,
  `RpcHandshake*`, `RpcPeerRef*`, `RpcPeerConnectionState*`, keep-alive,
  reconnect/backoff, `RpcMessage*` framing/limits.

Focus: WebSocket frame handling and size limits, message framing/parsing
robustness against malformed input, handshake validation (peer identity,
version negotiation, remote peer id), reconnect/backoff loops (unbounded retry,
thundering herd), peer registries that grow with remote-supplied keys,
connection teardown races, resource leaks on abort, cancellation propagation,
keep-alive/timeout handling.

---

## P2 — RPC call pipeline, routing, streams & method resolution

Scope:
- `src/ActualLab.Rpc/Infrastructure/` (everything not claimed by P1):
  inbound/outbound calls, call registries, `RpcMethodDef`/`RpcServiceDef`
  resolution, argument lists, headers, `RpcStream*`, `RpcObjectTracker*`,
  system calls (`RpcSystemCalls`), call routing/rerouting.
- `src/ActualLab.Rpc/Middlewares/`
- `src/ActualLab.Rpc/Internal/`
- `src/ActualLab.Rpc/Caching/`
- `src/ActualLab.Rpc/Attributes/`
- `src/ActualLab.Rpc/Diagnostics/`

Focus: correctness of service/method lookup from names received over the wire;
whether the separation between client-facing services and backend-only services
is enforced correctly everywhere; method filters; `RpcCallRouter`; system-call
handling; call-id collision/reuse; growth of the inbound call table; stream id
handling and cross-peer object trackers; argument count/type mismatch handling;
how much internal detail an error response carries back to the caller;
cancellation and completion races.

---

## P3 — Serialization (RPC + Core) & text/IO buffers

Scope:
- `src/ActualLab.Rpc/Serialization/`
- `src/ActualLab.Core/Serialization/`
- `src/ActualLab.Serialization.NerdbankMessagePack/`
- `src/ActualLab.Core/Text/`
- `src/ActualLab.Core/IO/`

Focus: deserialization that resolves types from names received over the wire
(`TypeRef`, type name parsing, `Type.GetType`-style lookups, allow lists);
polymorphic deserialization; MemoryPack / MessagePack / System.Text.Json
converter behaviour on malformed input; reconstruction of exception objects from
the wire; buffer/`Span` arithmetic; `ArrayPool` misuse (double-return, use after
return, not clearing sensitive data); allocation sized by a value read from the
wire; `ByteString`/UTF-8 handling; encoder/decoder state.

---

## P4 — Server hosting endpoints (ASP.NET Core + .NET Framework)

Scope:
- `src/ActualLab.Rpc.Server/`
- `src/ActualLab.Rpc.Server.NetFx/`
- `src/ActualLab.Fusion.Server/`
- `src/ActualLab.Fusion.Server.NetFx/`
- `src/ActualLab.RestEase/`

Focus: the WebSocket upgrade endpoint (origin checks / CSRF protection,
authentication, per-connection limits, client-id handling from the query
string), route/middleware registration, session id extraction from
cookies/headers/query and its validation, other HTTP endpoints exposed by
Fusion.Server, error responses that leak internals, anything that trusts a
header or query parameter without validating it.

---

## P5 — Fusion core: Computed, State, invalidation, operations, client cache

Scope:
- `src/ActualLab.Fusion/` **except** `Session/`, i.e.:
  `Computed*`, `State/`, `Interception/`, `Internal/`, `Operations/`,
  `Client/`, `UI/`, `Rpc/`, `Extensions/`, `Blazor/`, `Configuration/`,
  `Diagnostics/`, and the project-root `*.cs` files.

Focus: dependency-graph races (add/invalidate/dispose ordering), lost or
duplicated invalidations, `ComputedRegistry` growth and eviction, weak-reference
handling, the client-side computed cache (can a malformed or unexpected server
response corrupt it? can a stale entry be served after invalidation?), operation
log / completion handling, `State` update races and unobserved exceptions,
`async void` and fire-and-forget paths, cancellation token leaks, visibility of
`Computed` values across threads.

---

## P6 — Sessions, auth, extension services, EF Core & Redis persistence

Scope:
- `src/ActualLab.Fusion/Session/`
- `src/ActualLab.Fusion.Ext.Contracts/`
- `src/ActualLab.Fusion.Ext.Services/`
- `src/ActualLab.Fusion.EntityFramework/`
- `src/ActualLab.Fusion.EntityFramework.Npgsql/`
- `src/ActualLab.Fusion.EntityFramework.Redis/`
- `src/ActualLab.Redis/`
- `src/ActualLab.Fusion.Blazor/`
- `src/ActualLab.Fusion.Blazor.Authentication/`

Focus: session id generation (entropy, RNG choice), session validation and
lifetime, session fixation, whether a client-supplied session id is accepted
without verification, `IAuth` sign-in/sign-out and user identity/claims handling,
tenant/shard isolation in the EF layer, raw SQL built by string concatenation or
interpolation, operation log integrity, Redis key construction from user input,
trust placed in pub/sub messages, Blazor circuit/session binding and component
state that could outlive or cross a circuit.

---

## P7 — ActualLab.Core: async, locking, concurrency, collections, time

Scope:
- `src/ActualLab.Core/Async/`, `Locking/`, `Concurrency/`, `Channels/`,
  `Collections/`, `Pooling/`, `Caching/`, `Time/`, `Resilience/`,
  `Scalability/`, `Net/`, `OS/`, `Mathematics/`

Focus: lock-free code correctness (memory barriers, ABA, torn reads),
`AsyncLock`/reentrant lock correctness, deadlock potential, `TaskSource`/
`TaskCompletionSource` continuation hazards, `CancellationTokenSource` leaks and
disposal races, unbounded queues/caches, eviction correctness, thread-safety
claims that the implementation does not actually satisfy, clock/timer drift and
overflow, retry/backoff jitter using non-thread-safe `Random`, `ObjectPool`
cross-thread reuse.

---

## P8 — ActualLab.Core (rest), Interception, Generators, CommandR, Plugins

Scope:
- `src/ActualLab.Core/` remaining folders: `Reflection/`, `Conversion/`,
  `Compatibility/`, `Api/`, `Versioning/`, `DependencyInjection/`,
  `Generators/`, `Requirements/`, `Diagnostics/`, `Internal/`, `UnitOptions/`,
  `Comparison/`, `Trimming/`, `Rpc/`, and project-root `*.cs`.
- `src/ActualLab.Interception/`
- `src/ActualLab.Generators/`
- `src/ActualLab.CommandR/`
- `src/ActualLab.Plugins/`

Focus: reflection-based type/member resolution driven by strings; runtime proxy
generation in `ActualLab.Interception`; plugin discovery and assembly loading
from disk/config (this executes code, so the trust boundary matters);
`RandomStringGenerator` and other id generators (entropy, thread safety,
uniqueness); the `CommandR` pipeline (handler resolution, filters, and whether a
remotely-originated command can reach a handler it should not); DI resolution
patterns influenced by input; `Requirements` used as validation gates.

---

## P9 — TypeScript client (all packages)

Scope:
- `ts/packages/core/`, `ts/packages/rpc/`, `ts/packages/fusion/`,
  `ts/packages/fusion-rpc/`, `ts/packages/fusion-react/`
- plus `ts/*.ts` config files and `ts/e2e/` if relevant.
- Ignore `ts/node_modules` and `ts/dist` entirely.

Focus: how the WebSocket client handles unexpected or malformed server
messages; JSON parsing and **prototype pollution** (`__proto__` / `constructor`
keys assigned into objects); unbounded Map/Array growth keyed by server-supplied
ids; reconnect loops without backoff; unhandled promise rejections; timers and
listeners that are never removed (leaks in React components); `any`-typed
boundaries where a wrong-shaped message throws; DOM sinks that could render
untrusted strings (`innerHTML`, `dangerouslySetInnerHTML`, `eval`, `Function`,
dynamic `import()` of a server-supplied string); where session ids are stored
and how exposed that storage is; cross-tab/BroadcastChannel trust; React hook
subscription/cleanup bugs causing stale state or leaks.

It is also worth comparing the TypeScript RPC implementation against the C# one
in `src/ActualLab.Rpc` — protocol mismatches between the two are a rich source
of bugs.
