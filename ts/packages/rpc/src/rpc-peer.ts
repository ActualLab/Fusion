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
//     after reconnection.  TS uses transparent reconnect: outbound calls survive
//     disconnect, are re-sent on reconnect (stage-3 compute calls self-invalidate
//     on peer change instead of being re-sent).
//   - peerChangedToken / CancellationTokenSource — .NET threads a cancellation
//     token through all inbound calls that gets cancelled when the remote peer
//     changes identity.  TS clears the inbound tracker on peer change;
//     outbound calls survive disconnect and are re-sent on reconnect.
//   - Reset() — aborts RemoteObjects, SharedObjects, OutboundCalls, and clears
//     InboundCalls.  TS clears inbound tracker on peer change; outbound calls
//     are never rejected on disconnect (only on close/stop).
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
import { RpcClientPeerReconnectDelayer } from "./rpc-client-peer-reconnect-delayer.js";
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
import { RpcRemoteObjectTracker } from "./rpc-remote-object-tracker.js";
import { RpcSharedObjectTracker } from "./rpc-shared-object-tracker.js";
import { RpcStreamSender } from "./rpc-stream-sender.js";

/**
 * Default serialization format for RpcClientPeer connections.
 * TS uses plain JSON.stringify without polymorphic type wrapping,
 * so "json5np" (System.Text.Json, no polymorphism) is the correct match.
 */
export const DEFAULT_SERIALIZATION_FORMAT = "json5np";

/** Builds the WebSocket connection URL for an RpcClientPeer. */
export type RpcConnectionUrlResolver = (peer: RpcClientPeer) => string;

/** Default connection URL provider — appends `clientId` and `f` query parameters. */
export const defaultConnectionUrlResolver: RpcConnectionUrlResolver = (peer) => {
  const sep = peer.ref.includes('?') ? '&' : '?';
  return peer.ref + sep + `clientId=${peer.clientId}&f=${peer.serializationFormat}`;
};

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
  readonly remoteObjects = new RpcRemoteObjectTracker();
  readonly sharedObjects = new RpcSharedObjectTracker();

  readonly connected = new EventHandlerSet<void>();
  readonly disconnected = new EventHandlerSet<{ code: number; reason: string }>();

  protected _pendingSends: RpcOutboundCall[] = [];
  private _keepAliveTimer: ReturnType<typeof setInterval> | undefined;

  constructor(ref: string, hub: RpcHub) {
    this.ref = ref;
    this._hub = hub;
  }

  get hub(): RpcHub {
    return this._hub;
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
      this._connection = undefined;
      this._stopKeepAlive();
      this.disconnected.trigger(ev);
    });

    void conn.whenConnected.then(() => {
      this.connected.trigger();
      this._startKeepAlive();
    });
  }

  call(method: string, args?: unknown[], options?: RpcCallOptions): RpcOutboundCall {
    const callId = this.outbound.nextId();
    const outboundCall = options?.outboundCallFactory
      ? options.outboundCallFactory(callId, method)
      : new RpcOutboundCall(callId, method);

    const msg = serializeMessage(
      { Method: method, RelatedId: callId, CallType: options?.callTypeId ?? 0 },
      args,
    );
    outboundCall.serializedMessage = msg;
    if (this._connection !== undefined) {
      this.outbound.register(outboundCall);
      this._connection.send(msg);
    } else {
      this._pendingSends.push(outboundCall);
    }

    // Wire up caller-initiated cancellation → sends $sys.Cancel to remote peer
    const signal = options?.signal;
    if (signal !== undefined) {
      const peer = this;
      const hub = this._hub;
      const tracker = this.outbound;
      const onAbort = () => {
        if (tracker.remove(callId) !== undefined) {
          outboundCall.result.reject(new Error("Call cancelled."));
          outboundCall.onDisconnect();
          if (peer._connection !== undefined)
            hub.systemCallSender.cancel(peer._connection, callId);
        } else {
          const idx = peer._pendingSends.findIndex(c => c.callId === callId);
          if (idx !== -1) {
            peer._pendingSends.splice(idx, 1);
            outboundCall.result.reject(new Error("Call cancelled."));
            outboundCall.onDisconnect();
          }
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
    this.remoteObjects.disconnectAll();
    this.sharedObjects.disconnectAll();
    for (const call of this._pendingSends) {
      if (!call.result.isCompleted)
        call.result.reject(new Error("Peer closed."));
      call.onDisconnect();
    }
    this._pendingSends.length = 0;
    this.outbound.rejectAll(new Error("Peer closed."));
    this.outbound.invalidateAll();
    this._connection?.close();
    this._hub.peers.delete(this.ref);
  }

  /** Send any messages buffered while disconnected. Call after connection + handshake are ready. */
  protected _flushPendingSends(): void {
    if (this._pendingSends.length === 0 || this._connection === undefined) return;
    for (const call of this._pendingSends) {
      this.outbound.register(call);
      this._connection.send(call.serializedMessage);
    }
    this._pendingSends.length = 0;
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

    // Other system calls — delegate to hub's system call handler
    if (method.startsWith("$sys")) {
      this._hub.systemCallHandler.handle(message, args, this);
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
          if (methodDef?.stream === true && this._connection !== undefined) {
            // Stream method — create sender, send reference, pump items
            const sender = new RpcStreamSender(this);
            this.sharedObjects.register(sender);
            this._hub.systemCallSender.ok(this._connection, relatedId, sender.toRef());
            void sender.writeFrom(result as AsyncIterable<unknown>);
          } else if (!isNoWait && this._connection !== undefined) {
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
  private _reconnectsAt = 0;

  /** Base64url-encoded peer ID — matches .NET's RpcClientPeer.ClientId (Guid.ToBase64Url). */
  readonly clientId: string;
  /**
   * Serialization format key sent to the server via `f=` query parameter.
   * TS uses plain JSON.stringify (no polymorphic type wrapping), so `json5np`
   * (no-polymorphism) is the correct default.
   */
  readonly serializationFormat: string;
  /** Builds the WebSocket URL for this peer. Replace to customize URL construction. */
  connectionUrlResolver: RpcConnectionUrlResolver = defaultConnectionUrlResolver;
  readonly peerChanged = new EventHandlerSet<void>();
  readonly reconnectDelayer = new RpcClientPeerReconnectDelayer();
  readonly reconnectsAtChanged = new EventHandlerSet<void>();

  get reconnectsAt(): number { return this._reconnectsAt; }

  private _setReconnectsAt(value: number): void {
    if (this._reconnectsAt === value) return;
    this._reconnectsAt = value;
    this.reconnectsAtChanged.trigger();
  }

  constructor(hub: RpcHub, url: string, serializationFormat?: string) {
    super(url, hub);
    this.clientId = guidToBase64Url(this.id);
    this.serializationFormat = serializationFormat ?? DEFAULT_SERIALIZATION_FORMAT;
  }

  get connectionKind(): RpcPeerConnectionKind {
    return this._connectionKind;
  }

  /** One-shot connection for tests — no reconnection loop, no handshake. */
  connectWith(conn: RpcConnection): void {
    // Set up new connection and re-send outbound calls (treat as peer change —
    // no handshake exchange to determine same-peer, so stage-3 compute calls
    // are self-invalidated while regular in-flight calls are re-sent).
    this.setupConnection(conn);
    this._reconnect(true);
  }

  /** Start the reconnection loop — runs until disposed. */
  async run(wsFactory?: (url: string) => WebSocketLike): Promise<void> {
    const connUrl = this.connectionUrlResolver(this);
    while (!this._disposed) {
      try {
        const ws = wsFactory?.(connUrl) ?? new WebSocket(connUrl) as unknown as WebSocketLike;
        const conn = new RpcWebSocketConnection(ws);
        this._connectionKind = RpcPeerConnectionKind.Connecting;

        // Create a fresh handshake promise before setupConnection (which registers
        // the message handler).  The handler can only fire after the WS opens,
        // and we send our handshake below, so timing is safe.
        this._pendingHandshake = new PromiseSource<RemoteHandshake>();
        this.setupConnection(conn);
        // Keep _connection undefined until handshake completes — prevents
        // calls from being sent through the connection before the handshake
        // (which the .NET server cannot process).  Calls made during this
        // window go to _pendingSends and are flushed after handshake.
        this._connection = undefined;

        // Race connection open against close — if WS fails to connect,
        // whenConnected stays pending forever, so we must also watch for close.
        const closedRejection = new Promise<never>((_, reject) =>
          conn.closed.add(() => reject(new Error("Connection failed"))));
        closedRejection.catch(() => {}); // prevent unhandled rejection when conn closes normally
        await Promise.race([conn.whenConnected, closedRejection]);

        // Send our handshake, then wait for the server's response.
        this._hub.systemCallSender.handshake(conn, this.id, this._hub.hubId, ++this._handshakeIndex);
        const remoteHandshake = await Promise.race([
          this._pendingHandshake.promise,
          closedRejection,
        ]);
        this._pendingHandshake = undefined;

        // Peer change detection (like .NET's RpcHandshake.GetPeerChangeKind)
        const remoteHubId = remoteHandshake.RemoteHubId;
        let isPeerChanged = false;
        if (remoteHubId !== undefined) {
          isPeerChanged = this._lastRemoteHubId !== undefined && this._lastRemoteHubId !== remoteHubId;
          if (isPeerChanged) {
            // Server identity changed — clear inbound state
            this.inbound.clear();
            this.peerChanged.trigger();
          }
          this._lastRemoteHubId = remoteHubId;
        }

        // Activate the connection and re-send outbound calls.
        // All of this happens AFTER the handshake, so the server is ready to process calls.
        this._connection = conn;
        this._connectionKind = RpcPeerConnectionKind.Connected;
        this._tryIndex = 0;
        this._reconnect(isPeerChanged);

        // Wait until disconnected
        await new Promise<void>(r => conn.closed.add(() => r()));
      } catch {
        // connection failed or handshake failed
      }

      this._connectionKind = RpcPeerConnectionKind.Disconnected;
      if (this._disposed) break;

      this._tryIndex++;
      const delay = this.reconnectDelayer.getDelay(this._tryIndex);
      if (delay.isLimitExceeded) { this._disposed = true; break; }
      this._setReconnectsAt(delay.endsAt);
      try { await delay.promise; }
      finally { this._setReconnectsAt(0); }
    }
  }

  /** Re-send outbound calls after reconnection + flush pending sends.
   *  Stage-3 compute calls are always self-invalidated: without $sys.Reconnect
   *  protocol, the server's invalidation tracking is lost on disconnect, and
   *  re-sending would get a duplicate $sys.Ok that is ignored (PromiseSource
   *  already resolved). Self-invalidation forces a fresh recompute that
   *  establishes new invalidation tracking on the new connection.
   *  Regular in-flight calls are re-sent transparently. */
  private _reconnect(_isPeerChanged: boolean): void {
    if (this._connection === undefined) return;
    const conn = this._connection;

    // Handle remote objects on reconnect
    if (_isPeerChanged) {
      this.remoteObjects.disconnectAll();
    } else {
      this.remoteObjects.reconnectAll();
    }

    // Re-send existing tracker calls (self-invalidate stage-3 compute calls)
    const trackerCalls = [...this.outbound.values()];
    for (const call of trackerCalls) {
      if (!call.removeOnOk && call.result.isCompleted) {
        // Stage-3 compute call: self-invalidate, forcing fresh recompute
        call.onDisconnect();
        this.outbound.remove(call.callId);
      } else {
        // Regular call or in-flight compute call: re-send
        conn.send(call.serializedMessage);
      }
    }

    // Flush calls buffered while disconnected
    this._flushPendingSends();
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

/**
 * Convert a UUID string to base64url — matches .NET's Guid.ToBase64Url().
 * .NET Guid stores the first 3 groups in little-endian byte order.
 */
function guidToBase64Url(uuid: string): string {
  const hex = uuid.replace(/-/g, '');
  const bytes = new Uint8Array(16);

  // Group 1 (bytes 0-3): little-endian
  bytes[0] = parseInt(hex.slice(6, 8), 16);
  bytes[1] = parseInt(hex.slice(4, 6), 16);
  bytes[2] = parseInt(hex.slice(2, 4), 16);
  bytes[3] = parseInt(hex.slice(0, 2), 16);
  // Group 2 (bytes 4-5): little-endian
  bytes[4] = parseInt(hex.slice(10, 12), 16);
  bytes[5] = parseInt(hex.slice(8, 10), 16);
  // Group 3 (bytes 6-7): little-endian
  bytes[6] = parseInt(hex.slice(14, 16), 16);
  bytes[7] = parseInt(hex.slice(12, 14), 16);
  // Groups 4-5 (bytes 8-15): big-endian
  for (let i = 8; i < 16; i++)
    bytes[i] = parseInt(hex.slice(i * 2, i * 2 + 2), 16);

  const binary = String.fromCharCode(...bytes);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
