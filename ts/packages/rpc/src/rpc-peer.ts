// .NET counterparts:
//   RpcPeer (529 lines) — abstract WorkerBase that runs a reconnection loop in
//     OnRun().  Manages AsyncState<RpcPeerConnectionState> (a linked list of
//     immutable connection snapshots), handshake exchange, peerChanged detection,
//     message processing loop, maintenance tasks, Reset(), and SetConnectionState()
//     with thread-safe locking.
//   RpcClientPeer (56 lines) — calls GetConnection → WebSocket transport.
//   RpcServerPeer (54 lines) — receives connection via RpcServerConnectionHandler.
//   RpcPeerConnectionKind (48 lines) — None/Remote/Loopback/Local enum.
//   RpcPeerConnectionState (42 lines) — immutable record: Connection, Transport,
//     Handshake, OwnHandshake, ReaderTokenSource, Error, TryIndex.
//   RpcPeerChangeKind (11 lines) — Unchanged/ChangedToVeryFirst/Changed enum.
//   RpcHandshake (28 lines) — PeerId, ApiVersionSet, HubId, ProtocolVersion,
//     Index.  Has GetPeerChangeKind(lastHandshake) comparison method.
//   RpcClientPeerReconnectDelayer (35 lines) — exponential backoff profiles:
//     test (50ms), server (250ms), client (1s) with 1.5x growth, max 10s.
//
// Ported from .NET:
//   - Handshake exchange — run() sends $sys.Handshake on connect, waits for the
//     server's response.  RpcServerPeer._onHandshakeReceived() auto-replies.
//     Like .NET's OnRun() handshake sequence.
//   - Peer change detection — run() compares RemoteHubId from successive
//     handshakes to detect server restarts, triggering peerChanged + inbound.clear().
//     Like .NET's RpcHandshake.GetPeerChangeKind().
//   - Outbound call cancellation via AbortSignal — call(signal) registers an
//     abort listener that rejects the call, removes it from the tracker, and sends
//     $sys.Cancel to the server.  Like .NET's CancellationHandler + NotifyCancelled().
//
// Omitted from .NET:
//   - AsyncState<RpcPeerConnectionState> — immutable linked-list of connection
//     states that consumers can await via WhenConnected/WhenDisconnected.  TS uses
//     simpler EventHandlerSet events (connected/disconnected).  AsyncState enables
//     callers to block until a specific state transition, which is essential in
//     .NET's multi-threaded world but unnecessary in TS's single-threaded model.
//   - RpcPeerConnectionState record — snapshots Connection + Handshake + OwnHandshake
//     + ReaderTokenSource + Error + TryIndex.  TS tracks these as mutable fields
//     on the peer directly.
//   - Maintenance tasks (SharedObjects.Maintain, RemoteObjects.Maintain,
//     OutboundCalls.Maintain) — background loops that run while connected,
//     checking timeouts and managing object lifetimes.  TS has simpler keep-alive
//     timer; no shared/remote object tracking, no call timeout monitoring.
//   - Reconnect protocol (OutboundCalls.Reconnect) — stage-based call resumption
//     after reconnection.  TS replays entire calls.
//   - peerChangedToken / CancellationTokenSource — .NET threads a cancellation
//     token through all inbound calls that gets cancelled when the remote peer
//     changes identity.  TS rejects pending calls via rejectAll() on disconnect
//     and clears the inbound tracker on peer change.
//   - Reset() — aborts RemoteObjects, SharedObjects, OutboundCalls, and clears
//     InboundCalls.  TS does outbound.rejectAll() on disconnect + inbound.clear()
//     on peer change; no shared/remote object tracking.
//   - ServerMethodResolver / GetServerMethodResolver(handshake) — resolves method
//     defs using the remote peer's API version set + legacy names.  TS has no
//     versioning.
//   - ProcessMessage() — creates RpcInboundContext per message, dispatches via
//     MethodDef.InboundCallInvoker.  TS dispatches via _handleMessage →
//     handleSystemCall or RpcServiceHost.dispatch.
//   - RpcPeerRef / routing — .NET peers are keyed by RpcPeerRef (which encodes
//     client/server, route state, versions, serialization format).  TS peers are
//     keyed by string ref (URL for clients, "server://{uuid}" for servers).
//   - StopMode / ComputeAutoStopMode — controls behavior of inbound calls when
//     peer stops (cancel vs keep-incomplete).  TS has no stop mode.
//   - ConnectionKind detector (Remote/Loopback/Local/None) — .NET detects if peer
//     is in-process.  TS is always remote.
//   - Versions / VersionSet / API version negotiation — .NET exchanges version
//     sets during handshake for backward compatibility.  TS has no versioning.
//   - Diagnostics (CallLogger, CallLogLevel, DebugLog) — per-peer logging/tracing.
//   - Per-call timeout monitoring (OutboundCallTracker.Maintain) — .NET checks
//     elapsed time against per-method timeouts.  TS relies on WebSocket-level
//     disconnect + rejectAll.
//   - Inbound call cancellation propagation — when $sys.Cancel is received, .NET
//     cancels the inbound call's CancellationTokenSource, aborting the running
//     service method.  TS removes the call from the inbound tracker but does not
//     yet propagate cancellation to the service handler (would require AbortSignal
//     threading through RpcServiceHost.dispatch).

import { EventHandlerSet, PromiseSource } from "@actuallab/core";
import { RpcWebSocketConnection, type RpcConnection, type WebSocketLike } from "./rpc-connection.js";
import {
  RpcOutboundCall,
  RpcOutboundCallTracker,
  RpcInboundCall,
  RpcInboundCallTracker,
} from "./rpc-call-tracker.js";
import { RpcSystemCalls, type RpcMessage } from "./rpc-message.js";
import {
  serializeMessage,
  deserializeMessage,
} from "./rpc-serialization.js";
import type { RpcHub } from "./rpc-hub.js";

/** Options for RpcPeer.call() — allows custom call types and cancellation. */
export interface RpcCallOptions {
  /** Wire CallType field (0 = regular). */
  callTypeId?: number;
  /** Factory for creating custom outbound call instances (e.g. compute calls). */
  outboundCallFactory?: (id: number, method: string) => RpcOutboundCall;
  /** AbortSignal for caller-initiated cancellation. */
  signal?: AbortSignal;
}

/** Data extracted from an inbound $sys.Handshake message. */
export interface RemoteHandshake {
  RemotePeerId?: string;
  RemoteHubId?: string;
  ProtocolVersion?: number;
  Index?: number;
}

/** Base class for RPC peers — handles bidirectional message dispatch. */
export abstract class RpcPeer {
  /** Routing/addressing key — used as key in hub.peers (URL for clients, "server://{uuid}" for servers). */
  readonly ref: string;
  /** Auto-generated GUID — used in handshakes only. */
  readonly id: string = crypto.randomUUID();
  protected _hub: RpcHub;
  protected _connection: RpcConnection | undefined;
  readonly outbound = new RpcOutboundCallTracker();
  readonly inbound = new RpcInboundCallTracker();

  readonly connected = new EventHandlerSet<void>();
  readonly disconnected = new EventHandlerSet<{ code: number; reason: string }>();

  private _keepAliveTimer: ReturnType<typeof setInterval> | undefined;

  constructor(ref: string, hub: RpcHub) {
    this.ref = ref;
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

  call(method: string, args?: unknown[], options?: RpcCallOptions): RpcOutboundCall {
    if (this._connection === undefined)
      throw new Error("Not connected.");

    const callId = this.outbound.nextId();
    const outboundCall = options?.outboundCallFactory
      ? options.outboundCallFactory(callId, method)
      : new RpcOutboundCall(callId, method);

    this.outbound.register(outboundCall);

    const msg = serializeMessage(
      { Method: method, RelatedId: callId, CallType: options?.callTypeId ?? 0 },
      args,
    );
    this._connection.send(msg);

    // Wire up caller-initiated cancellation → sends $sys.Cancel to remote peer
    const signal = options?.signal;
    if (signal !== undefined) {
      const conn = this._connection;
      const hub = this._hub;
      const tracker = this.outbound;
      const onAbort = () => {
        if (tracker.remove(callId) !== undefined) {
          outboundCall.result.reject(new Error("Call cancelled."));
          outboundCall.onDisconnect();
          hub.systemCallSender.cancel(conn, callId);
        }
      };
      signal.addEventListener("abort", onAbort, { once: true });
      // Clean up listener when the call completes normally
      outboundCall.result.promise
        .then(() => signal.removeEventListener("abort", onAbort))
        .catch(() => signal.removeEventListener("abort", onAbort));
    }

    return outboundCall;
  }

  callNoWait(method: string, args?: unknown[]): void {
    if (this._connection === undefined) return; // silently drop
    const msg = serializeMessage({ Method: method, RelatedId: 0 }, args);
    this._connection.send(msg); // never throws
  }

  close(): void {
    this._stopKeepAlive();
    this.outbound.invalidateAll();
    this._connection?.close();
    this._hub.peers.delete(this.ref);
  }

  /** Override in subclasses to handle the remote peer's handshake response. */
  protected _onHandshakeReceived(_handshake: RemoteHandshake): void {
    // Default: no-op.  RpcClientPeer resolves a pending promise;
    // RpcServerPeer sends its own handshake back.
  }

  private _handleMessage(raw: string): void {
    const { message, args } = deserializeMessage(raw);
    const method = message.Method ?? "";

    // Handshake — dispatch to subclass handler
    if (method === RpcSystemCalls.handshake) {
      const handshake = args[0] as RemoteHandshake | undefined;
      if (handshake !== undefined)
        this._onHandshakeReceived(handshake);
      return;
    }

    // Other system calls — delegate to hub (allows FusionHub to intercept)
    if (method.startsWith("$sys")) {
      this._hub.handleSystemCall(message, args, this.outbound, this.inbound);
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

/** Client-side RPC peer — initiates WebSocket connection. ref = URL. */
export class RpcClientPeer extends RpcPeer {
  private _disposed = false;
  private _connectionKind = RpcPeerConnectionKind.Disconnected;
  private _tryIndex = 0;
  private _handshakeIndex = 0;
  private _lastRemoteHubId: string | undefined;
  private _pendingHandshake: PromiseSource<RemoteHandshake> | undefined;

  readonly peerChanged = new EventHandlerSet<void>();

  constructor(hub: RpcHub, url: string) {
    super(url, hub);
  }

  get connectionKind(): RpcPeerConnectionKind {
    return this._connectionKind;
  }

  /** One-shot connection for tests — no reconnection loop, no handshake. */
  connectWith(conn: RpcConnection): void {
    // Invalidate stage-3 compute calls from the previous connection
    this.outbound.invalidateAll();
    this.setupConnection(conn);
  }

  /** Start the reconnection loop — runs until disposed. */
  async run(wsFactory?: (url: string) => WebSocketLike): Promise<void> {
    while (!this._disposed) {
      try {
        const ws = wsFactory?.(this.ref) ?? new WebSocket(this.ref) as unknown as WebSocketLike;
        const conn = new RpcWebSocketConnection(ws);
        this._connectionKind = RpcPeerConnectionKind.Connecting;

        // Invalidate stage-3 compute calls from the previous connection
        this.outbound.invalidateAll();

        // Create a fresh handshake promise before setupConnection (which registers
        // the message handler).  The handler can only fire after the WS opens,
        // and we send our handshake below, so timing is safe.
        this._pendingHandshake = new PromiseSource<RemoteHandshake>();
        this.setupConnection(conn);
        await conn.whenConnected;

        // Send our handshake, then wait for the server's response.
        this._hub.systemCallSender.handshake(conn, this.id, this._hub.hubId, ++this._handshakeIndex);
        const remoteHandshake = await Promise.race([
          this._pendingHandshake.promise,
          // Break out if the connection closes during handshake
          new Promise<never>((_, reject) =>
            conn.closed.add(() => reject(new Error("Connection closed during handshake")))),
        ]);
        this._pendingHandshake = undefined;

        // Peer change detection (like .NET's RpcHandshake.GetPeerChangeKind)
        const remoteHubId = remoteHandshake.RemoteHubId;
        if (remoteHubId !== undefined) {
          if (this._lastRemoteHubId !== undefined && this._lastRemoteHubId !== remoteHubId) {
            // Server identity changed — clear state
            this.inbound.clear();
            this.peerChanged.trigger();
          }
          this._lastRemoteHubId = remoteHubId;
        }

        this._connectionKind = RpcPeerConnectionKind.Connected;
        this._tryIndex = 0;

        // Wait until disconnected
        await new Promise<void>(r => conn.closed.add(() => r()));
      } catch {
        // connection failed or handshake failed
      }

      this._connectionKind = RpcPeerConnectionKind.Disconnected;
      if (this._disposed) break;

      // Backoff delay: 1s, 1.5s, 2.25s, ... up to 10s
      this._tryIndex++;
      await new Promise(r => setTimeout(r, Math.min(1000 * Math.pow(1.5, this._tryIndex - 1), 10_000)));
    }
  }

  protected override _onHandshakeReceived(handshake: RemoteHandshake): void {
    this._pendingHandshake?.resolve(handshake);
  }

  override close(): void {
    this._disposed = true;
    super.close();
  }
}

/** Server-side RPC peer — wraps an accepted connection. ref = "server://{uuid}". */
export class RpcServerPeer extends RpcPeer {
  constructor(hub: RpcHub, ref: string) {
    super(ref, hub);
  }

  /** Accept an incoming connection — sets up message handling and handshake response. */
  accept(conn: RpcConnection): void {
    this.setupConnection(conn);
  }

  protected override _onHandshakeReceived(_handshake: RemoteHandshake): void {
    // Client sent its handshake → respond with our own
    if (this._connection !== undefined) {
      this._hub.systemCallSender.handshake(this._connection, this.id, this._hub.hubId, 0);
    }
  }
}
