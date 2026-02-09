import { EventHandlerSet } from "@actuallab/core";
import { splitFrame } from "./rpc-serialization.js";
import type { RpcConnection } from "./rpc-connection.js";

/** MessagePort-based RpcConnection — for in-process testing without WebSocket mocks. */
export class RpcMessageChannelConnection implements RpcConnection {
  private _port: MessagePort;
  private _open = true;

  readonly messageReceived = new EventHandlerSet<string>();
  readonly closed = new EventHandlerSet<{ code: number; reason: string }>();
  readonly whenConnected: Promise<void> = Promise.resolve(); // immediately connected

  constructor(port: MessagePort) {
    this._port = port;
    port.onmessage = (ev: MessageEvent) => {
      const data = typeof ev.data === "string" ? ev.data : String(ev.data);
      for (const msg of splitFrame(data))
        if (msg.length > 0) this.messageReceived.trigger(msg);
    };
  }

  get isOpen(): boolean {
    return this._open;
  }

  send(serializedMessage: string): void {
    if (!this._open) return;
    try {
      this._port.postMessage(serializedMessage);
    } catch {
      // never fail
    }
  }

  close(code?: number, reason?: string): void {
    if (!this._open) return;
    this._open = false;
    this._port.close();
    this.closed.trigger({ code: code ?? 1000, reason: reason ?? "" });
  }
}

/** Creates a pair of connected RpcMessageChannelConnections for testing. */
export function createMessageChannelPair(): [RpcMessageChannelConnection, RpcMessageChannelConnection] {
  const channel = new MessageChannel();
  return [
    new RpcMessageChannelConnection(channel.port1),
    new RpcMessageChannelConnection(channel.port2),
  ];
}
