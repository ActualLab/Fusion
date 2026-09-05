// .NET counterparts:
//   RpcCall (29 lines) — base class: MethodDef, Id, NoWait flag, Lock.
//   RpcOutboundCall (437 lines) — tracks an outbound call's full lifecycle:
//     result via AsyncTaskMethodBuilder, CacheInfoCapture, CancellationHandler,
//     CompletedStage (for Reliable reconnection), StartedAt (for timeouts),
//     hashing for Match, tracing, routing, and retry logic.
//   RpcInboundCall (298 lines) — tracks an inbound call: deserialization,
//     middleware-chain invocation, cancellation via linked CTS, stage-based
//     re-processing for reconnection, sending the result back.
//   RpcOutboundCallTracker — thread-safe ConcurrentDictionary of outbound calls with:
//     HandleDisconnect() (per-outage watcher owning ConnectTimeout), Maintain() loop
//     (RunTimeout/DelayTimeout + logging), Reconnect() protocol (stage-based call
//     resumption), Abort(), TryReroute().
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
//   (Timeout monitoring IS ported — see RpcPeer._maintainOutboundCalls. .NET splits it
//   between RpcOutboundCallTracker.HandleDisconnect, a per-outage watcher that owns
//   ConnectTimeout, and Maintain, which owns RunTimeout while connected; TS runs both
//   rules from one timer.)
//   - CompletedStage / RpcCallStage — supports Reliable call type's stage-based
//     reconnection protocol where the server tells the client which stage each
//     call reached, avoiding re-execution of completed stages.  TS replays entire
//     calls on reconnect (simpler, adequate for browser client).
//   - Reconnect() protocol — outbound tracker sends $sys.Reconnect with compressed
//     call IDs grouped by stage; server responds with "unknown" IDs that need
//     re-sending.  Not ported; TS uses full replay.
//   - Abort()'s final-error latch — .NET latches the error before its (single) sweep, so a
//     call registered afterwards is completed by Register rather than left pending. TS does a
//     single-pass rejectAll() without the latch; the exposure is much smaller, since close()
//     drops the peer from the hub map synchronously instead of keeping it for PeerRemoveDelay.
//   - TryReroute() — checks if the call's peer route has changed (load-balancer
//     rerouting) and sets RpcRerouteException.  TS has no routing layer.
//   - IsLongLiving / _longLivingCalls — .NET sets this on compute calls only; it governs
//     whether completing a call releases its cancellation handler and trace
//     (CompleteAndUnregister vs CompleteKeepRegistered), not whether timeouts apply -
//     HandleDisconnect and Maintain both iterate every call. TS has no equivalent.
//   - RpcInboundCall: middleware-chain invocation, deserialization with polymorphic
//     argument handling, stage-based TryReprocess, CancellationTokenSource per
//     call.  TS inbound calls are dispatched directly by RpcServiceHost; no
//     middleware, no per-call CTS, no stage tracking.
//   - RpcCall.Lock (monitor-based synchronisation) — .NET uses lock(this) for
//     thread-safe result setting.  TS is single-threaded; no locking needed.

import { PromiseSource } from '@actuallab/core';
import { RpcCallStage } from './rpc-call-stage.js';
import { RpcRemoteExecutionMode } from './rpc-service-def.js';
import type { RpcCallTimeouts } from './rpc-call-timeouts.js';

/** Tracks a pending outbound RPC call. */
export class RpcOutboundCall {
    readonly callId: number;
    readonly method: string;
    readonly result = new PromiseSource<unknown>();
    /** Bitfield of RpcRemoteExecutionMode flags controlling reconnect/resend behavior. */
    readonly remoteExecutionMode: number;

    /** Whether to remove this call from the tracker on $sys.Ok. Default: true.
     *  Subclasses (e.g. compute calls) override to false to stay in tracker for invalidation. */
    readonly removeOnOk: boolean = true;

    /** The serialized wire data — stored for re-sending on reconnect. */
    serializedWireData: string | Uint8Array = '';

    /** Per-call connect/run timeouts enforced by the peer's maintenance loop
     *  (R12). `undefined` means unbounded (the query default). */
    timeouts: RpcCallTimeouts | undefined;

    /** `Date.now()` when this call was created (used for the connect timeout). */
    readonly startedAt = Date.now();

    /** `Date.now()` when this call was last put on the wire, or 0 if it hasn't
     *  been sent yet (used for the run timeout). */
    sentAt = 0;

    /**
     * Bitfield of {@link RpcCallStage} flags indicating how far this call
     * has progressed. Used by the `$sys.Reconnect` protocol to tell the
     * remote peer which calls are still pending its attention.
     *
     * - 0: brand new, no result yet (the peer is expected to be actively processing).
     * - `ResultReady` (1): `$sys.Ok` received for this call.
     * - `Invalidated` (3): result was invalidated (compute calls only).
     * - `Unregistered` (0x1000): removed from the tracker.
     */
    completedStage = 0;

    constructor(callId: number, method: string, remoteExecutionMode = 7) {
        this.callId = callId;
        this.method = method;
        this.remoteExecutionMode = remoteExecutionMode;
    }

    /**
     * Returns the stage to report to the remote peer via `$sys.Reconnect`,
     * or `null` if this call should not be reconciled and must be aborted.
     *
     * Mirrors .NET `RpcOutboundCall.GetReconnectStage` at
     * src/ActualLab.Rpc/Infrastructure/RpcOutboundCall.cs.
     */
    getReconnectStage(isPeerChanged: boolean): number | null {
        const mode = this.remoteExecutionMode;
        if (!(mode & RpcRemoteExecutionMode.AllowReconnect)) return null;
        if (isPeerChanged && !(mode & RpcRemoteExecutionMode.AllowResend)) return null;
        return this.completedStage;
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
        if (call !== undefined) {
            this._calls.delete(callId);
            call.completedStage |= RpcCallStage.Unregistered;
        }
        return call;
    }

    values(): IterableIterator<RpcOutboundCall> {
        return this._calls.values();
    }

    activeCallIds(): number[] {
        return [...this._calls.keys()];
    }

    /** Calls handed to the transport at least once. Must be snapshotted BEFORE the
     *  peer enters `Connected` — that flushes every queued call, making queued and
     *  in-flight calls indistinguishable. Mirrors .NET's `GetSentCalls`. */
    getSentCalls(): RpcOutboundCall[] {
        const calls: RpcOutboundCall[] = [];
        for (const call of this._calls.values())
            if (call.sentAt !== 0)
                calls.push(call);
        return calls;
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
    /** 0 = still processing, 1 = result computed and sent at least once. */
    completedStage = 0;
    private _resend: (() => void) | undefined;
    private readonly _abortController = new AbortController();

    constructor(callId: number, method: string, args: unknown[]) {
        this.callId = callId;
        this.method = method;
        this.args = args;
    }

    /** Aborted when the remote peer cancels this call via `$sys.Cancel` (R17).
     *  Threaded into the service handler so it can abort in-flight work. */
    get signal(): AbortSignal {
        return this._abortController.signal;
    }

    /** True once the remote peer cancelled this call — the response is then
     *  suppressed even though the call may still be registered (R9 dedup). */
    get isCancelled(): boolean {
        return this._abortController.signal.aborted;
    }

    cancel(): void {
        this._abortController.abort();
    }

    // Records the closure that re-sends this call's already-computed result;
    // marks the call as completed. Mirrors .NET's post-`ProcessStage1Plus` state.
    setResult(resend: () => void): void {
        this._resend = resend;
        this.completedStage = 1;
    }

    // Re-sends the result to the remote peer if it was already computed.
    // A resent frame for an in-flight call is a no-op — the original dispatch
    // will send the result when it completes. Mirrors .NET
    // `RpcInboundCall.TryReprocess`.
    resendResult(): void {
        if (this.completedStage >= 1)
            this._resend?.();
    }
}

/** Manages inbound calls by their RelatedId. */
export class RpcInboundCallTracker {
    private _calls = new Map<number, RpcInboundCall>();
    private _completedIds: number[] = [];

    /** Cap on retained completed calls — see `RpcLimits.completedInboundCallsLimit`. */
    completedCallsLimit = 1000;

    get size(): number {
        return this._calls.size;
    }

    register(call: RpcInboundCall): void {
        this._calls.set(call.callId, call);
    }

    // Registers `call` unless its id is already tracked, in which case the
    // existing call is returned. Mirrors .NET `RpcInboundCallTracker.GetOrRegister`.
    getOrRegister(call: RpcInboundCall): RpcInboundCall {
        const existing = this._calls.get(call.callId);
        if (existing !== undefined)
            return existing;

        this._calls.set(call.callId, call);
        return call;
    }

    // Keeps the completed `call` registered for duplicate-frame dedup, evicting
    // the oldest completed calls beyond `completedCallsLimit`. Deviation from
    // .NET (which unregisters a call once its result is sent): TS peers
    // blind-resend after reconnect, so a completed call must stay resolvable
    // to avoid re-executing the handler — but only within a bounded window.
    markCompleted(call: RpcInboundCall): void {
        if (this._calls.get(call.callId) !== call)
            return;

        this._completedIds.push(call.callId);
        while (this._completedIds.length > this.completedCallsLimit) {
            const id = this._completedIds.shift()!;
            this._calls.delete(id);
        }
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
        this._completedIds.length = 0;
    }
}
