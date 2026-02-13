// .NET counterpart:
//   RpcServerConnectionHandler — ASP.NET Core middleware that accepts WebSocket
//     upgrades, creates an RpcServerPeer via Hub.GetServerPeer(), and feeds the
//     connection.  In .NET this is integrated with Kestrel's middleware pipeline.
//
// Omitted from .NET:
//   - ASP.NET Core middleware integration (HttpContext, WebSocket upgrade) — TS
//     defines a minimal abstract interface; actual WebSocket server libraries
//     (e.g. `ws`) implement it.
//   - Authentication / HttpContext.User propagation — .NET middleware extracts
//     auth context from the HTTP upgrade request.  TS has no auth layer.
//   - Connection properties (PropertyBag) from HTTP headers — .NET attaches
//     request metadata to the connection.  TS has no such mechanism.

import type { WebSocketLike } from "./rpc-connection.js";

/** Abstract WebSocket server interface — implement with `ws` or other library. */
export interface WebSocketServer {
  onConnection(handler: (ws: WebSocketLike) => void): void;
  close(): void;
}
