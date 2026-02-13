// .NET counterparts:
//   RpcCall (29 lines) — base class: MethodDef, Id, NoWait flag, Lock.
//   RpcOutboundCall (437 lines) — tracks an outbound call's full lifecycle:
//     result via AsyncTaskMethodBuilder, CacheInfoCapture, CancellationHandler,
//     CompletedStage (for Reliable reconnection), StartedAt (for timeouts),
//     hashing for Match, tracing, routing, and retry logic.
//   RpcInboundCall (298 lines) — tracks an inbound call: deserialization,
//     middleware-chain invocation, cancellation via linked CTS, stage-based
//     re-processing for reconnection, sending the result back.
//   RpcOutboundCallTracker (255 lines) — thread-safe ConcurrentDictionary of
//     outbound calls with: Maintain() loop (timeout monitoring + logging),
//     Reconnect() protocol (stage-based call resumption), Abort(), TryReroute().
//   RpcInboundCallTracker (64 lines) — ConcurrentDictionary, GetOrRegister,
//     Unregister, Clear.
//
// Omitted from .NET:
//   - AsyncTaskMethodBuilder / typed RpcOutboundCall<T> — .NET creates a typed
//     task builder for each call's return type.  TS uses PromiseSource<unknown>
//     (untyped) because TypeScript erases generics at runtime.
//   - CancellationHandler (CancellationToken.Register → Cancel) — .NET registers
//     a callback per outbound call.  TS implements equivalent via optional
//     AbortSignal on RpcPeer.call(); the cancel handler rejects the promise,
//     removes from tracker, and sends $sys.Cancel — same behavior as .NET's
//     Cancel() + NotifyCancelled().
//   - StartedAt / CpuTimestamp / Timeout monitoring — .NET's Maintain() loop
//     checks elapsed time against per-method timeouts every CallTimeoutCheckPeriod.
//     TS has no per-call timeout mechanism; the WebSocket-level disconnect +
//     rejectAll handles stuck calls.
//   - CompletedStage / RpcCallStage — supports Reliable call type's stage-based
//     reconnection protocol where the server tells the client which stage each
//     call reached, avoiding re-execution of completed stages.  TS replays entire
//     calls on reconnect (simpler, adequate for browser client).
//   - Reconnect() protocol — outbound tracker sends $sys.Reconnect with compressed
//     call IDs grouped by stage; server responds with "unknown" IDs that need
//     re-sending.  Not ported; TS uses full replay.
//   - Abort() with multi-pass retry — .NET iterates 3+ times with delays to
//     catch late-registered calls.  TS does a single-pass rejectAll().
//   - TryReroute() — checks if the call's peer route has changed (load-balancer
//     rerouting) and sets RpcRerouteException.  TS has no routing layer.
//   - IsLongLiving / _longLivingCalls — separate tracking for calls that outlive
//     normal timeouts (e.g. streaming).  Not needed without RpcStream.
//   - RpcInboundCall: middleware-chain invocation, deserialization with polymorphic
//     argument handling, stage-based TryReprocess, CancellationTokenSource per
//     call.  TS inbound calls are dispatched directly by RpcServiceHost; no
//     middleware, no per-call CTS, no stage tracking.
//   - RpcCall.Lock (monitor-based synchronisation) — .NET uses lock(this) for
//     thread-safe result setting.  TS is single-threaded; no locking needed.

import { PromiseSource } from "@actuallab/core";

/** Tracks a pending outbound RPC call. */
export class RpcOutboundCall {
  readonly callId: number;
  readonly method: string;
  readonly result = new PromiseSource<unknown>();

  /** Whether to remove this call from the tracker on $sys.Ok. Default: true.
   *  Subclasses (e.g. compute calls) override to false to stay in tracker for invalidation. */
  readonly removeOnOk: boolean = true;

  constructor(callId: number, method: string) {
    this.callId = callId;
    this.method = method;
  }

  /** Called when the connection is lost. Subclasses can override to resolve invalidation promises. */
  onDisconnect(): void {
    // no-op by default
  }
}

/** Manages outbound calls by their RelatedId. */
export class RpcOutboundCallTracker {
  private _calls = new Map<number, RpcOutboundCall>();
  private _nextId = 1;

  get size(): number {
    return this._calls.size;
  }

  nextId(): number {
    return this._nextId++;
  }

  register(call: RpcOutboundCall): void {
    this._calls.set(call.callId, call);
  }

  get(callId: number): RpcOutboundCall | undefined {
    return this._calls.get(callId);
  }

  remove(callId: number): RpcOutboundCall | undefined {
    const call = this._calls.get(callId);
    if (call !== undefined) this._calls.delete(callId);
    return call;
  }

  activeCallIds(): number[] {
    return [...this._calls.keys()];
  }

  /** Reject all pending calls with the given error.
   *  Stage-3 compute calls (result resolved, awaiting invalidation) are kept in the tracker. */
  rejectAll(error: Error): void {
    for (const [id, call] of this._calls) {
      if (!call.removeOnOk && call.result.isCompleted) {
        // Stage-3 compute call — keep it for later invalidation on reconnect/stop
        continue;
      }
      call.result.reject(error);
      call.onDisconnect();
      this._calls.delete(id);
    }
  }

  /** Invalidate all remaining stage-3 calls (on reconnect or peer stop). */
  invalidateAll(): void {
    for (const call of this._calls.values()) {
      call.onDisconnect();
    }
    this._calls.clear();
  }

  clear(): void {
    this._calls.clear();
  }
}

/** Tracks an incoming inbound RPC call. */
export class RpcInboundCall {
  readonly callId: number;
  readonly method: string;
  readonly args: unknown[];

  constructor(callId: number, method: string, args: unknown[]) {
    this.callId = callId;
    this.method = method;
    this.args = args;
  }
}

/** Manages inbound calls by their RelatedId. */
export class RpcInboundCallTracker {
  private _calls = new Map<number, RpcInboundCall>();

  get size(): number {
    return this._calls.size;
  }

  register(call: RpcInboundCall): void {
    this._calls.set(call.callId, call);
  }

  get(callId: number): RpcInboundCall | undefined {
    return this._calls.get(callId);
  }

  remove(callId: number): RpcInboundCall | undefined {
    const call = this._calls.get(callId);
    if (call !== undefined) this._calls.delete(callId);
    return call;
  }

  clear(): void {
    this._calls.clear();
  }
}
