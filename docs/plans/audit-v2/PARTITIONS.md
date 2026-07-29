# Review partitions (round 2)

Each reviewer owns exactly one partition. Read **only** your partition deeply,
but you may read anything else in the repo as *context* (callers, base classes,
tests, docs) when you need it to prove or disprove a finding. Report findings
that live inside your partition. If you find something clearly severe outside
your partition, report it anyway in a trailing `## Out-of-partition findings`
section.

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

Focus: WebSocket frame handling and size limits, message framing/parsing,
handshake trust decisions (peer identity, version negotiation, remote peer id),
reconnect/backoff loops (unbounded retry, thundering herd), peer registry growth
keyed by attacker-controlled values, connection teardown races, resource leaks on
abort, cancellation propagation, keep-alive/timeout handling.

---

## P2 — RPC call pipeline, routing, streams & access control

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

Focus: **can a remote client invoke something it should not?** — service/method
lookup from wire-supplied names, backend-vs-client service separation, method
filters, `RpcCallRouter`, system-call handling, call-id collision/reuse, inbound
call table growth (DoS), stream id handling and cross-peer object trackers,
argument count/type mismatch handling, error/exception content sent back to the
remote peer (info leak), cancellation and completion races.

---

## P3 — Serialization (RPC + Core) & text/IO buffers

Scope:
- `src/ActualLab.Rpc/Serialization/`
- `src/ActualLab.Core/Serialization/`
- `src/ActualLab.Serialization.NerdbankMessagePack/`
- `src/ActualLab.Core/Text/`
- `src/ActualLab.Core/IO/`

Focus: unsafe/polymorphic deserialization, type resolution from wire-supplied
type names (`TypeRef`, type name parsing, `Type.GetType` style lookups, allow
lists), MemoryPack / MessagePack / System.Text.Json converter correctness on
hostile input, exception-type reconstruction from the wire, buffer/`Span`
arithmetic, `ArrayPool` misuse (returning rented arrays twice, using after
return, not clearing sensitive data), unbounded allocation driven by a
wire-supplied length, `ByteString`/`Utf8` handling, encoder/decoder state.

---

## P4 — Server hosting endpoints (ASP.NET Core + .NET Framework)

Scope:
- `src/ActualLab.Rpc.Server/`
- `src/ActualLab.Rpc.Server.NetFx/`
- `src/ActualLab.Fusion.Server/`
- `src/ActualLab.Fusion.Server.NetFx/`
- `src/ActualLab.RestEase/`

Focus: the WebSocket upgrade endpoint (origin checks / CSRF, authentication,
per-connection limits, client-id handling from query string), route/middleware
registration, session id extraction from cookies/headers/query and its
validation, HTTP endpoints exposed by Fusion.Server, error responses that leak
internals, anything that trusts a header or query parameter.

---

## P5 — Fusion core: Computed, State, invalidation, operations, client cache

Scope:
- `src/ActualLab.Fusion/` **except** `Session/`, i.e.:
  `Computed*`, `State/`, `Interception/`, `Internal/`, `Operations/`,
  `Client/`, `UI/`, `Rpc/`, `Extensions/`, `Blazor/`, `Configuration/`,
  `Diagnostics/`, and the project-root `*.cs` files.

Focus: dependency-graph races (add/invalidate/dispose ordering), lost or
duplicated invalidations, `ComputedRegistry` growth and eviction, weak-reference
handling, the client-side computed cache (can a server response poison it? can a
stale entry be served after invalidation?), operation log / completion handling,
`State` update races and unobserved exceptions, `async void` and fire-and-forget
paths, cancellation token leaks, `Computed` value visibility across threads.

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
lifetime, session fixation, whether a client can supply an arbitrary session id
and act as another user, auth service (`IAuth`, sign-in/sign-out, user identity,
claims) authorization checks, tenant/shard isolation in the EF layer, raw SQL /
string interpolation into SQL, operation log poisoning, Redis key construction
from user input, pub/sub message trust, Blazor circuit/session binding and
component state leaking across circuits.

---

## P7 — ActualLab.Core: async, locking, concurrency, collections, time

Scope:
- `src/ActualLab.Core/Async/`
- `src/ActualLab.Core/Locking/`
- `src/ActualLab.Core/Concurrency/`
- `src/ActualLab.Core/Channels/`
- `src/ActualLab.Core/Collections/`
- `src/ActualLab.Core/Pooling/`
- `src/ActualLab.Core/Caching/`
- `src/ActualLab.Core/Time/`
- `src/ActualLab.Core/Resilience/`
- `src/ActualLab.Core/Scalability/`
- `src/ActualLab.Core/Net/`
- `src/ActualLab.Core/OS/`
- `src/ActualLab.Core/Mathematics/`

Focus: lock-free code correctness (memory barriers, ABA, torn reads),
`AsyncLock`/`ReentrantAsyncLock` correctness, deadlock potential, `TaskSource`/
`TaskCompletionSource` continuation-on-capturing-context hazards,
`CancellationTokenSource` leaks and disposal races, unbounded queues/caches,
eviction correctness, custom collection thread-safety claims vs. reality,
clock/timer drift and overflow, retry/backoff jitter using non-thread-safe
`Random`, `ObjectPool` cross-thread reuse.

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

Focus: reflection-based type/member resolution driven by strings, dynamic
assembly/proxy generation (`ActualLab.Interception` runtime emit + generated
proxies), plugin discovery and loading (arbitrary assembly load from disk/config
= code execution), `RandomStringGenerator`/id generators (entropy, thread
safety, uniqueness), `CommandR` pipeline (handler resolution, filters, whether a
remote command can reach a handler it should not), DI service resolution
patterns that can be influenced by input, `Requirements` used as security checks.

---

## P9 — TypeScript client (all packages)

Scope:
- `ts/packages/core/`
- `ts/packages/rpc/`
- `ts/packages/fusion/`
- `ts/packages/fusion-rpc/`
- `ts/packages/fusion-react/`
- plus `ts/*.ts` config files and `ts/e2e/` if relevant.

Focus: WebSocket client handling of hostile server messages, JSON parsing and
**prototype pollution** (`__proto__` / `constructor` keys assigned into objects),
unbounded Map/Array growth keyed by server-supplied ids (memory DoS),
reconnect loops without backoff, unhandled promise rejections, timers/listeners
that are never removed (leaks in React components), `any`-typed boundaries where
a wrong-shaped message crashes the client, XSS-capable sinks (`innerHTML`,
`dangerouslySetInnerHTML`, `eval`, `Function`, dynamic `import()` of a
server-supplied string), session id storage in `localStorage`/cookies and its
exposure, cross-tab/BroadcastChannel trust, React hook subscription/cleanup bugs
causing stale state or leaks.
