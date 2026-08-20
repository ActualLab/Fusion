# @actuallab/fusion-rpc

[![npm](https://img.shields.io/npm/v/@actuallab/fusion-rpc)](https://www.npmjs.com/package/@actuallab/fusion-rpc)
[![Documentation](https://img.shields.io/badge/Documentation-6B5B95)](https://fusion.actuallab.net/PartTS-FusionRpc)
[![License](https://img.shields.io/npm/l/@actuallab/fusion-rpc)](https://github.com/ActualLab/Fusion/blob/master/LICENSE)

The bridge between [`@actuallab/fusion`](https://www.npmjs.com/package/@actuallab/fusion) and
[`@actuallab/rpc`](https://www.npmjs.com/package/@actuallab/rpc): `FusionHub` — a compute-aware RPC
hub whose client proxies cache results locally and drop them the moment the .NET server says the
value changed. This is the TypeScript equivalent of Fusion's client-side RPC infrastructure
(Remote Compute Service interceptor, `RpcComputeCallType`).

**This is the package you want** if your goal is "call [Fusion](https://fusion.actuallab.net/)
Compute Services running on a .NET server from a JS/TS client, and get real-time updates".

## Installation

```bash
npm install @actuallab/fusion-rpc
```

ESM-first with a CJS fallback; ships its own `.d.ts`. Pulls in `@actuallab/core`,
`@actuallab/fusion`, and `@actuallab/rpc`.

## Quick start

```ts
import { FusionHub, defineComputeService } from '@actuallab/fusion-rpc';
import { RpcClientPeer, RpcPeerStateMonitor } from '@actuallab/rpc';

// 1. Define the service — the name must match the .NET interface name exactly.
const TodoApiDef = defineComputeService('ITodoApi', {
    // Compute methods: cached locally, invalidated by the server
    Get: { args: ['', ''] },                     // (session, id) → TodoItem
    ListIds: { args: ['', 0] },                  // (session, count) → string[]
    GetSummary: { args: [''] },                  // (session) → TodoSummary

    // Commands: opt out of compute caching
    AddOrUpdate: { args: [{}], callTypeId: 0 },
    Remove: { args: [{}], callTypeId: 0 },
});

// 2. Hub + peer. The RpcClientPeer ctor starts the connect/reconnect loop.
const hub = new FusionHub();
const peer = new RpcClientPeer(hub, 'ws://localhost:5005/rpc/ws');
hub.addPeer(peer);

// 3. Typed client proxy — addClient is idempotent per (peer, service),
//    so every consumer shares one Computed and one invalidation stream.
const api = hub.addClient<ITodoApi>(peer, TodoApiDef);

// 4. Optional: connection status for the UI
const monitor = new RpcPeerStateMonitor(peer);

// Cached until the server invalidates it
const ids = await api.ListIds('~', 10);
```

Render it with
[`useComputedState`](https://www.npmjs.com/package/@actuallab/fusion-react) and the component
refreshes itself whenever the server-side data changes.

## How invalidation flows

1. The client calls a compute method — `FusionHub` sends it with `CallType = 1`.
2. The server computes, responds with `$sys.Ok`; the client caches the result locally.
3. Server-side data changes and the server-side `Computed<T>` is invalidated.
4. The server sends `$sys-c.Invalidate` for that call id.
5. The client invalidates its local replica; the invalidation cascades into every dependent
   `@computeMethod` and `ComputedState<T>` — and React re-renders.

An `Invalidate` that arrives *before* the result retries the call transparently (up to 3 attempts
while connected). On disconnect, compute replicas self-invalidate instead of being re-sent, since
the server's tracking for them is gone; regular in-flight calls are re-sent as usual.

## API surface

| API | Description |
|-----|-------------|
| `FusionHub` | `RpcHub` + compute-aware proxies, invalidation wiring, `acceptConnection(ws)` for hosting |
| `defineComputeService(name, methods)` | Service definition where methods default to `callTypeId: 1` (compute); use `callTypeId: 0` for commands |
| `FUSION_CALL_TYPE_ID` | The compute call type id (`1`) |
| `RpcOutboundComputeCall` | The outbound call type that carries invalidation handling |

## Notes

- **Cancellation.** A caller's `AbortSignal` travels through `AsyncContext` (`abortSignalKey`);
  aborting sends `$sys.Cancel`, and the resulting cancellation error is never cached.
- **Hosting compute services in Node.** `hub.addService(def, impl)` wraps compute methods and wires
  invalidation to `$sys-c.Invalidate`. Use a class with `@computeMethod` members whenever service
  methods call each other — a plain-object impl calls them as raw functions and loses the
  dependency edges.

## Documentation

- [`@actuallab/fusion-rpc` reference](https://fusion.actuallab.net/PartTS-FusionRpc) — full setup example, invalidation and reconnect details
- [TypeScript port overview](https://fusion.actuallab.net/PartTS)
- [TodoApp sample (React + Fusion)](https://github.com/ActualLab/Fusion.Samples/tree/master/src/TodoApp)

## License

MIT — see [LICENSE](https://github.com/ActualLab/Fusion/blob/master/LICENSE).
