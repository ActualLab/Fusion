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

/** Wraps a WebSocket — handles frame splitting and message queueing. */
export class RpcConnection {
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
    if (this._ws.readyState === WebSocketState.OPEN) {
      this._ws.send(serializedMessage);
    } else {
      this._sendBuffer.push(serializedMessage);
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
    this._ws.send(frame);
  }
}
