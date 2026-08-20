# @actuallab/rpc

[![npm](https://img.shields.io/npm/v/@actuallab/rpc)](https://www.npmjs.com/package/@actuallab/rpc)
[![Documentation](https://img.shields.io/badge/Documentation-6B5B95)](https://fusion.actuallab.net/PartTS-Rpc)
[![License](https://img.shields.io/npm/l/@actuallab/rpc)](https://github.com/ActualLab/Fusion/blob/master/LICENSE)

TypeScript client (and Node.js host) for
[`ActualLab.Rpc`](https://fusion.actuallab.net/PartR) — the WebSocket RPC layer behind
[Fusion](https://fusion.actuallab.net/). It speaks the same wire protocol as the .NET
implementation, and handles connection management, serialization, streaming, system calls, and
automatic reconnection for you.

Use it on its own for plain RPC. For compute calls with server-driven invalidation, use
[`@actuallab/fusion-rpc`](https://www.npmjs.com/package/@actuallab/fusion-rpc), which extends this
package's `RpcHub`.

## Installation

```bash
npm install @actuallab/rpc
```

ESM-first with a CJS fallback; ships its own `.d.ts`. Works in browsers and in Node.js (supply a
`ws` WebSocket via `peer.webSocketFactory`).

## Quick start

```ts
import { RpcHub, RpcClientPeer, defineRpcService, RpcType } from '@actuallab/rpc';

// The name must match the .NET interface name exactly.
const SimpleServiceDef = defineRpcService('ISimpleService', {
    Greet: { args: [''] },                              // (message) → string
    Counter: { args: [], returns: RpcType.stream },     // () → RpcStream<number>
    Ping: { args: [''], returns: RpcType.noWait },      // fire-and-forget
});

const hub = new RpcHub();
const peer = new RpcClientPeer(hub, 'ws://localhost:5005/rpc/ws'); // auto-starts
const client = hub.addClient<ISimpleService>(peer, SimpleServiceDef);

console.log(await client.Greet('World'));

for await (const i of await client.Counter()) {
    if (i > 10) break;   // sends AckEnd to the server
}

client.Ping('hello');    // returns void, dropped while disconnected
```

`args` is only read for its `length` — the values are placeholders that document the signature.
Services can also be declared with the `@rpcService` / `@rpcMethod` decorators, which work for both
clients and server-side implementations.

## API surface

| API | Description |
|-----|-------------|
| `RpcHub` | Peers, registered services, shared reconnect delayer, `addClient` / `addService` / `close` |
| `RpcClientPeer` | A client connection with handshake, buffering, and exponential-backoff reconnect (100 ms → 10 s) |
| `RpcServerPeer`, `RpcWebSocketConnection` | Hosting side: accept an inbound WebSocket |
| `defineRpcService`, `rpcService`, `rpcMethod`, `RpcType` | Service definitions |
| `RpcStream<T>`, `toRpcStream` | `AsyncIterable<T>` streams with ack-based backpressure and resume-after-reconnect |
| `RpcPeerStateMonitor`, `RpcPeerStateKind` | Connection-status state machine with `JustConnected` / `JustDisconnected` grace periods, for UI |
| `RpcCallTimeouts`, `RpcLimits` | Per-call timeouts and connection-lifecycle timing (keep-alive, handshake, …) |
| `genericErrorFilter` | Replaces outbound error messages with a generic one when your host serves RPC |

## Notes

- **Reconnection is automatic.** The delayer lives on the hub and is shared by all client peers;
  `hub.reconnectDelayer.cancelDelays()` forces an immediate retry (useful for a "Reconnect now"
  button). A connection that drops within 15 s of connecting doesn't reset the backoff.
- **Serialization.** `json5np` (default) or MessagePack (`msgpack6` / `msgpack6c`), selected with
  the `?f=` URL key. MemoryPack and polymorphic payloads are not supported — they fail loudly.
- **Node.js.** Construct the peer with `mustStart = false`, set `peer.webSocketFactory`, then call
  `peer.start()`.
- **Security.** Serve RPC over `https`/`wss`: reconnect proofs need `crypto.subtle`, which is
  unavailable in insecure browser contexts. If your Node host *serves* RPC, consider
  `hub.systemCallSender.errorFilter = genericErrorFilter` so handler exception text (paths,
  connection strings) doesn't reach callers.

## Documentation

- [`@actuallab/rpc` reference](https://fusion.actuallab.net/PartTS-Rpc) — full API tables, reconnect proof, wire protocol
- [TypeScript port overview](https://fusion.actuallab.net/PartTS)
- [ActualLab.Rpc](https://fusion.actuallab.net/PartR) and [RpcStream](https://fusion.actuallab.net/PartR-RpcStream) — the concepts, in .NET terms

## License

MIT — see [LICENSE](https://github.com/ActualLab/Fusion/blob/master/LICENSE).
