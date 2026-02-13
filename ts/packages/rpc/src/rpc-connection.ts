// .NET counterparts:
//   RpcConnection — thin wrapper around RpcTransport + PropertyBag.  RpcTransport
//     is an abstract IAsyncEnumerable<RpcInboundMessage> + Send() that can be
//     WebSocket-based or in-memory.  The transport also handles frame batching and
//     back-pressure via a Channel<T> send queue.
//
// Omitted from .NET:
//   - PropertyBag on connection — used in .NET for per-connection metadata (e.g.
//     authentication info attached by middleware).  TS has no middleware pipeline,
//     so no need for an extensible property bag.
//   - IsLocal flag — .NET distinguishes local loopback connections (same-process
//     server) from remote ones.  TS is always a remote client.
//   - RpcTransport as IAsyncEnumerable — .NET reads inbound messages via
//     async iteration with cancellation.  TS uses event-based delivery
//     (messageReceived handler) because browser WebSocket API is event-driven.
//   - Channel<RpcOutboundMessage> send queue with backpressure — .NET buffers
//     outbound messages in an async channel that the transport drains.  TS sends
//     synchronously on the WebSocket; if the socket isn't ready, messages are
//     buffered in _sendBuffer (CONNECTING) or silently dropped (CLOSING/CLOSED).
//     JS is single-threaded so there's no contention, and WebSocket.send() itself
//     handles buffering at the OS level.
//   - IAsyncDisposable — .NET transports are disposable resources.  TS connections
//     are closed via close() and garbage-collected.

import { PromiseSource, EventHandlerSet } from "@actuallab/core";
import { splitFrame, serializeFrame } from "./rpc-serialization.js";

/** Abstract WebSocket interface — works with both browser WebSocket and Node.js ws. */
export interface WebSocketLike {
  readonly readyState: number;
  send(data: string): void;
  close(code?: number, reason?: string): void;
  onopen: ((ev: unknown) => void) | null;
  onmessage: ((ev: { data: unknown }) => void) | null;
  onclose: ((ev: { code: number; reason: string }) => void) | null;
  onerror: ((ev: unknown) => void) | null;
}

export const WebSocketState = {
  CONNECTING: 0,
  OPEN: 1,
  CLOSING: 2,
  CLOSED: 3,
} as const;

/** Abstract RPC connection — transport-agnostic interface for sending/receiving messages. */
export interface RpcConnection {
  readonly isOpen: boolean;
  readonly whenConnected: Promise<void>;
  readonly messageReceived: EventHandlerSet<string>;
  readonly closed: EventHandlerSet<{ code: number; reason: string }>;
  send(serializedMessage: string): void;
  close(code?: number, reason?: string): void;
}

/** WebSocket-based RpcConnection — handles frame splitting and message queueing. */
export class RpcWebSocketConnection implements RpcConnection {
  private _ws: WebSocketLike;
  private _sendBuffer: string[] = [];
  private _connected = new PromiseSource<void>();

  readonly messageReceived = new EventHandlerSet<string>();
  readonly closed = new EventHandlerSet<{ code: number; reason: string }>();
  readonly error = new EventHandlerSet<unknown>();

  constructor(ws: WebSocketLike) {
    this._ws = ws;

    if (ws.readyState === WebSocketState.OPEN) {
      this._connected.resolve();
      this._flush();
    }

    ws.onopen = () => {
      this._connected.resolve();
      this._flush();
    };

    ws.onmessage = (ev) => {
      const data = typeof ev.data === "string" ? ev.data : String(ev.data);
      const messages = splitFrame(data);
      for (const msg of messages) {
        if (msg.length > 0) this.messageReceived.trigger(msg);
      }
    };

    ws.onclose = (ev) => {
      this.closed.trigger({ code: ev.code, reason: ev.reason });
    };

    ws.onerror = (ev) => {
      this.error.trigger(ev);
    };
  }

  get isOpen(): boolean {
    return this._ws.readyState === WebSocketState.OPEN;
  }

  get whenConnected(): Promise<void> {
    return this._connected.promise;
  }

  send(serializedMessage: string): void {
    try {
      if (this._ws.readyState === WebSocketState.OPEN)
        this._ws.send(serializedMessage);
      else if (this._ws.readyState === WebSocketState.CONNECTING)
        this._sendBuffer.push(serializedMessage);
      // CLOSING/CLOSED: silently drop
    } catch {
      // Swallow — disconnect event handles cleanup
    }
  }

  sendBatch(messages: string[]): void {
    const frame = serializeFrame(messages);
    this.send(frame);
  }

  close(code?: number, reason?: string): void {
    this._ws.close(code, reason);
  }

  private _flush(): void {
    if (this._sendBuffer.length === 0) return;
    const frame = serializeFrame(this._sendBuffer);
    this._sendBuffer = [];
    try {
      this._ws.send(frame);
    } catch {
      // Swallow — disconnect event handles cleanup
    }
  }
}
