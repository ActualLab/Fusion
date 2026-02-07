import type { WebSocketLike } from "./rpc-connection.js";

/** Abstract WebSocket server interface — implement with `ws` or other library. */
export interface WebSocketServer {
  onConnection(handler: (ws: WebSocketLike) => void): void;
  close(): void;
}
