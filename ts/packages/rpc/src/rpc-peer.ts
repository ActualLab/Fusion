import { EventHandlerSet } from "@actuallab/core";
import { RpcWebSocketConnection, type RpcConnection, type WebSocketLike } from "./rpc-connection.js";
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
import { handleSystemCall } from "./rpc-system-call-handler.js";
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

  protected setupConnection(conn: RpcConnection): void {
    this._connection = conn;

    conn.messageReceived.add((raw) => this._handleMessage(raw));
    conn.closed.add((ev) => {
      this._stopKeepAlive();
      this.outbound.rejectAll(new Error(`Connection closed: ${ev.reason}`));
      this.disconnected.trigger(ev);
    });

    void conn.whenConnected.then(() => {
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

  callNoWait(method: string, args?: unknown[]): void {
    if (this._connection === undefined) return; // silently drop
    const msg = serializeMessage({ Method: method, RelatedId: 0 }, args);
    this._connection.send(msg); // never throws
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
      return;
    }

    // Regular inbound call — dispatch to service host
    this._handleInbound(message, args);
  }

  private _handleInbound(message: RpcMessage, args: unknown[]): void {
    const method = message.Method ?? "";
    const relatedId = message.RelatedId ?? 0;

    const call = new RpcInboundCall(relatedId, method, args);
    const methodDef = this._hub.serviceHost.getMethodDef(method);

    // For noWait calls: don't register in tracker, don't send response
    const isNoWait = methodDef?.noWait === true;
    if (!isNoWait) {
      this.inbound.register(call);
    }

    // Dispatch to the hub's service host
    const serviceHost = this._hub.serviceHost;
    if (serviceHost !== undefined) {
      void (async () => {
        try {
          const context = this._connection !== undefined
            ? { __rpcDispatch: true as const, callId: relatedId, connection: this._connection }
            : undefined;
          const result = await serviceHost.dispatch(method, args, context);
          if (!isNoWait && this._connection !== undefined) {
            this._hub.systemCallSender.ok(this._connection, relatedId, result);
          }
        } catch (e) {
          if (!isNoWait && this._connection !== undefined) {
            this._hub.systemCallSender.error(this._connection, relatedId, e);
          }
        } finally {
          if (!isNoWait) {
            this.inbound.remove(relatedId);
          }
        }
      })();
    }
  }

  private _startKeepAlive(): void {
    this._keepAliveTimer = setInterval(() => {
      if (this._connection !== undefined) {
        this._hub.systemCallSender.keepAlive(this._connection, this.outbound.activeCallIds());
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

/** Connection state for RpcClientPeer. */
export const enum RpcPeerConnectionKind {
  Disconnected = 0,
  Connecting = 1,
  Connected = 2,
}

/** Client-side RPC peer — initiates WebSocket connection. */
export class RpcClientPeer extends RpcPeer {
  private _url: string;
  private _disposed = false;
  private _connectionKind = RpcPeerConnectionKind.Disconnected;
  private _tryIndex = 0;
  private _handshakeIndex = 0;
  private _lastRemoteHubId: string | undefined;

  readonly peerChanged = new EventHandlerSet<void>();

  constructor(id: string, hub: RpcHub, url: string) {
    super(id, hub);
    this._url = url;
  }

  get connectionKind(): RpcPeerConnectionKind {
    return this._connectionKind;
  }

  /** One-shot connection for tests — no reconnection loop. */
  connectWith(conn: RpcConnection): void {
    this.setupConnection(conn);
  }

  /** Start the reconnection loop — runs until disposed. */
  async run(wsFactory?: (url: string) => WebSocketLike): Promise<void> {
    while (!this._disposed) {
      try {
        const ws = wsFactory?.(this._url) ?? new WebSocket(this._url) as unknown as WebSocketLike;
        const conn = new RpcWebSocketConnection(ws);
        this._connectionKind = RpcPeerConnectionKind.Connecting;
        this.setupConnection(conn);
        await conn.whenConnected;

        // Exchange handshakes
        this._hub.systemCallSender.handshake(conn, this.id, this._hub.hubId, ++this._handshakeIndex);

        this._connectionKind = RpcPeerConnectionKind.Connected;
        this._tryIndex = 0;

        // Wait until disconnected
        await new Promise<void>(r => conn.closed.add(() => r()));
      } catch {
        // connection failed
      }

      this._connectionKind = RpcPeerConnectionKind.Disconnected;
      if (this._disposed) break;

      // Backoff delay: 1s, 1.5s, 2.25s, ... up to 10s
      this._tryIndex++;
      await new Promise(r => setTimeout(r, Math.min(1000 * Math.pow(1.5, this._tryIndex - 1), 10_000)));
    }
  }

  override close(): void {
    this._disposed = true;
    super.close();
  }
}

/** Server-side RPC peer — wraps an accepted connection. */
export class RpcServerPeer extends RpcPeer {
  constructor(id: string, hub: RpcHub, conn: RpcConnection) {
    super(id, hub);
    this.setupConnection(conn);
  }
}
