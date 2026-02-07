import { EventHandlerSet } from "@actuallab/core";
import { RpcConnection, type WebSocketLike } from "./rpc-connection.js";
import {
  RpcOutboundCall,
  RpcOutboundCallTracker,
  RpcOutboundComputeCall,
  RpcInboundCall,
  RpcInboundCallTracker,
} from "./rpc-call-tracker.js";
import type { RpcMessage } from "./rpc-message.js";
import {
  serializeMessage,
  deserializeMessage,
} from "./rpc-serialization.js";
import { handleSystemCall, sendOk, sendError, sendKeepAlive, sendHandshake } from "./rpc-system-calls.js";
import type { RpcHub } from "./rpc-hub.js";

/** Base class for RPC peers — handles bidirectional message dispatch. */
export abstract class RpcPeer {
  readonly id: string;
  protected _hub: RpcHub;
  protected _connection: RpcConnection | undefined;
  readonly outbound = new RpcOutboundCallTracker();
  readonly inbound = new RpcInboundCallTracker();

  readonly connected = new EventHandlerSet<void>();
  readonly disconnected = new EventHandlerSet<{ code: number; reason: string }>();

  private _keepAliveTimer: ReturnType<typeof setInterval> | undefined;

  constructor(id: string, hub: RpcHub) {
    this.id = id;
    this._hub = hub;
  }

  get connection(): RpcConnection | undefined {
    return this._connection;
  }

  get isConnected(): boolean {
    return this._connection?.isOpen ?? false;
  }

  protected setupConnection(ws: WebSocketLike): void {
    this._connection = new RpcConnection(ws);

    this._connection.messageReceived.add((raw) => this._handleMessage(raw));
    this._connection.closed.add((ev) => {
      this._stopKeepAlive();
      this.disconnected.trigger(ev);
    });

    void this._connection.whenConnected.then(() => {
      this.connected.trigger();
      this._startKeepAlive();
    });
  }

  call(method: string, args?: unknown[], compute = false): RpcOutboundCall {
    if (this._connection === undefined)
      throw new Error("Not connected.");

    const callId = this.outbound.nextId();
    const outboundCall = compute
      ? new RpcOutboundComputeCall(callId, method)
      : new RpcOutboundCall(callId, method);

    this.outbound.register(outboundCall);

    const msg = serializeMessage(
      { Method: method, RelatedId: callId },
      args,
    );
    this._connection.send(msg);
    return outboundCall;
  }

  sendHandshake(peerId: string, hubId: string, index: number): void {
    if (this._connection === undefined) return;
    sendHandshake(this._connection, peerId, hubId, index);
  }

  close(): void {
    this._stopKeepAlive();
    this._connection?.close();
  }

  private _handleMessage(raw: string): void {
    const { message, args } = deserializeMessage(raw);
    const method = message.Method ?? "";

    // System calls
    if (method.startsWith("$sys")) {
      handleSystemCall(message, args, this.outbound);
      // Also dispatch to inbound handler for server-side system call processing
      this._handleInbound(message, args);
      return;
    }

    // Regular inbound call — dispatch to service host
    this._handleInbound(message, args);
  }

  private _handleInbound(message: RpcMessage, args: unknown[]): void {
    const method = message.Method ?? "";
    const relatedId = message.RelatedId ?? 0;

    // Skip system calls that were already handled by handleSystemCall
    if (method.startsWith("$sys")) return;

    const call = new RpcInboundCall(relatedId, method, args);
    this.inbound.register(call);

    // Dispatch to the hub's service host
    const serviceHost = this._hub.serviceHost;
    if (serviceHost !== undefined) {
      void (async () => {
        try {
          const context = this._connection !== undefined
            ? { __rpcDispatch: true as const, callId: relatedId, connection: this._connection }
            : undefined;
          const result = await serviceHost.dispatch(method, args, context);
          if (this._connection !== undefined) {
            sendOk(this._connection, relatedId, result);
          }
        } catch (e) {
          if (this._connection !== undefined) {
            sendError(this._connection, relatedId, e);
          }
        } finally {
          this.inbound.remove(relatedId);
        }
      })();
    }
  }

  private _startKeepAlive(): void {
    this._keepAliveTimer = setInterval(() => {
      if (this._connection !== undefined) {
        sendKeepAlive(this._connection, this.outbound.activeCallIds());
      }
    }, 15_000);
  }

  private _stopKeepAlive(): void {
    if (this._keepAliveTimer !== undefined) {
      clearInterval(this._keepAliveTimer);
      this._keepAliveTimer = undefined;
    }
  }
}

/** Client-side RPC peer — initiates WebSocket connection. */
export class RpcClientPeer extends RpcPeer {
  private _url: string;

  constructor(id: string, hub: RpcHub, url: string) {
    super(id, hub);
    this._url = url;
  }

  connect(wsFactory?: (url: string) => WebSocketLike): void {
    const ws = wsFactory !== undefined
      ? wsFactory(this._url)
      : new WebSocket(this._url) as unknown as WebSocketLike;
    this.setupConnection(ws);
  }

  connectWith(ws: WebSocketLike): void {
    this.setupConnection(ws);
  }
}

/** Server-side RPC peer — wraps an accepted WebSocket connection. */
export class RpcServerPeer extends RpcPeer {
  constructor(id: string, hub: RpcHub, ws: WebSocketLike) {
    super(id, hub);
    this.setupConnection(ws);
  }
}
