import { PromiseSource, RingBuffer } from '@actuallab/core';
import type { RpcObjectId, IRpcObject } from './rpc-object.js';
import { RpcObjectKind } from './rpc-object.js';
import type { RpcPeer } from './rpc-peer.js';
import { RpcStream } from './rpc-stream.js';

/** Default ack period for server-side streams. */
const DEFAULT_ACK_PERIOD = 256;
/** Default ack advance for server-side streams. */
const DEFAULT_ACK_ADVANCE = 128;

/**
 * Server-side RPC stream producer — sends items to a remote consumer via
 * $sys.I / $sys.B / $sys.End system calls.
 *
 * Wire protocol (server perspective):
 * - Server returns the stream reference string as $sys.Ok
 * - Client sends Ack(0, hostId) to start the stream
 * - Server sends I (single item) / B (batch) messages
 * - Client acks every ackPeriod items for flow control
 * - Server sends End to signal completion (with optional error)
 * - Client sends AckEnd to acknowledge completion
 *
 * The pump is ACK-driven: the main loop blocks on `_whenAckReady` until a
 * client ACK is queued, processes it (updating `_lastAckedIndex`, rewinding
 * `_index` on mustReset, trimming the replay buffer), then sends items up
 * to `ack.nextIndex + ackAdvance`. While the peer is disconnected no ACKs
 * arrive, so the source is never pulled — the sender naturally pauses
 * instead of spinning.
 *
 * Direct port of .NET `RpcSharedStream<T>` at
 * src/ActualLab.Rpc/Infrastructure/RpcSharedStream.cs — including the
 * separate non-real-time / real-time pump variants.
 */
export class RpcStreamSender<T> implements IRpcObject {
    private static readonly _emptyGuid = '00000000-0000-0000-0000-000000000000';

    readonly id: RpcObjectId;
    readonly kind = RpcObjectKind.Local;
    readonly allowReconnect: boolean;
    readonly isRealTime: boolean;
    readonly canSkipTo: (item: T) => boolean;
    readonly peer: RpcPeer;
    readonly ackPeriod: number;
    readonly ackAdvance: number;
    /**
     * If true, the source was produced by a factory `(abortSignal) => AsyncIterable<T>`
     * and may honor the AbortSignal to exit gracefully. `disconnect()` first aborts
     * the signal and gives the source up to `RpcStream.disconnectGracePeriodMs` ms
     * before force-closing via `iterator.return()`.
     *
     * If false, the source is a plain AsyncIterable that cannot observe the signal,
     * so `disconnect()` force-closes immediately.
     */
    readonly sourceUsesAbortSignal: boolean;

    // -- Pump state --
    /** Next stream index that will be sent (matches .NET's `index`). */
    private _nextIndex = 0;
    private _ended = false;
    private _started = new PromiseSource<void>();
    /** Highest `nextIndex` observed in an ACK so far (matches .NET's `lastAckNextIndex` idiom). */
    private _lastAckedIndex = 0;
    private _abortController = new AbortController();
    private _iterator: AsyncIterator<T> | null = null;

    // -- ACK queue (analog of .NET's Channel<(long, bool)>) --
    private _acks: { nextIndex: number; mustReset: boolean }[] = [];
    private _whenAckReady: PromiseSource<void> | null = null;

    constructor(
        peer: RpcPeer,
        ackPeriod = DEFAULT_ACK_PERIOD,
        ackAdvance = DEFAULT_ACK_ADVANCE,
        allowReconnect = true,
        isRealTime = false,
        canSkipTo: (item: T) => boolean = () => true,
        sourceUsesAbortSignal = false,
    ) {
        const localId = peer.sharedObjects.nextId();
        this.id = { hostId: peer.hub.hubId, localId };
        this.allowReconnect = allowReconnect;
        this.isRealTime = isRealTime;
        this.canSkipTo = canSkipTo;
        this.peer = peer;
        this.ackPeriod = ackPeriod;
        this.ackAdvance = ackAdvance;
        this.sourceUsesAbortSignal = sourceUsesAbortSignal;
    }

    /** AbortSignal that is aborted when the sender is disconnected. */
    get abortSignal(): AbortSignal {
        return this._abortController.signal;
    }

    /** Returns the stream reference string for the $sys.Ok response. */
    toRef(): string {
        return `${this.id.hostId},${this.id.localId},${this.ackPeriod},${this.ackAdvance},${this.allowReconnect ? '1' : '0'},${this.isRealTime ? '1' : '0'}`;
    }

    /**
     * Called by system call handler when $sys.Ack is received from the client.
     *
     * Mirrors .NET `RpcSharedStream<T>.OnAck` at
     * src/ActualLab.Rpc/Infrastructure/RpcSharedStream.cs:68-101.
     */
    onAck(nextIndex: number, hostId: string): void {
        const mustReset = hostId !== '' && hostId !== RpcStreamSender._emptyGuid;
        if (!this._started.isCompleted) this._started.resolve();
        this._acks.push({ nextIndex, mustReset });
        if (this._whenAckReady) {
            this._whenAckReady.resolve();
            this._whenAckReady = null;
        }
    }

    /** Called by system call handler when $sys.AckEnd is received from the client. */
    onAckEnd(_hostId: string): void {
        this.disconnect();
    }

    /**
     * Send a single item to the client at the current `_nextIndex`.
     *
     * Advances `_nextIndex` unconditionally — even if the peer is disconnected
     * the item is considered "sent" from the pump's perspective so the replay
     * buffer indexing stays consistent. If a connection is absent, the wire
     * message is dropped; the item will be resent from the replay buffer
     * after a reconnect (client sends `Ack(N, mustReset=true)`).
     */
    sendItem(item: T): void {
        if (this._ended) return;
        const conn = this.peer.connection;
        if (conn) {
            this.peer.hub.systemCallSender.item(
                conn, this.peer.format, this.id.localId, this._nextIndex, item,
            );
        }
        this._nextIndex++;
    }

    /** Send a batch of items to the client. See {@link sendItem} for disconnect semantics. */
    sendBatch(items: T[]): void {
        if (this._ended || items.length === 0) return;
        const conn = this.peer.connection;
        if (conn) {
            this.peer.hub.systemCallSender.batch(
                conn, this.peer.format, this.id.localId, this._nextIndex, items,
            );
        }
        this._nextIndex += items.length;
    }

    /** Signal stream completion to the client. */
    sendEnd(error?: Error | null): void {
        if (this._ended) return;
        this._ended = true;
        const conn = this.peer.connection;
        if (!conn) return;
        // .NET ExceptionInfo is a non-nullable value type, so we must always
        // emit a valid map shape (empty TypeRef+Message for the "no error" case).
        const errorInfo = error
            ? { TypeRef: 'System.Exception', Message: error.message }
            : { TypeRef: '', Message: '' };
        this.peer.hub.systemCallSender.end(
            conn, this.peer.format, this.id.localId, this._nextIndex, errorInfo,
        );
    }

    /**
     * Consume an AsyncIterable and send all items to the client.
     *
     * Dispatches to the non-real-time or real-time pump, matching .NET
     * `RpcSharedStream<T>.OnRun` at
     * src/ActualLab.Rpc/Infrastructure/RpcSharedStream.cs:105-108.
     */
    async writeFrom(source: AsyncIterable<T>): Promise<void> {
        await this._started.promise;
        if (this._ended) return;

        const iterator = source[Symbol.asyncIterator]();
        this._iterator = iterator;
        // Tracks whether the generator has reached a terminal state (done=true
        // or thrown). When true, its finally block has already run and the
        // iterator reference can be cleared. When false, disconnect()'s
        // scheduled iterator.return() is what drives it to its finally.
        const state: _PumpState = { iteratorDone: false };
        try {
            if (this.isRealTime) {
                await this._runRealTime(iterator, state);
            } else {
                await this._runNonRealTime(iterator, state);
            }
        } finally {
            if (state.iteratorDone && this._iterator === iterator) {
                this._iterator = null;
            }
        }
    }

    /**
     * Non-real-time pump — back-pressure through buffered ACKs.
     *
     * Direct port of .NET `OnRunNonRealTime` at
     * src/ActualLab.Rpc/Infrastructure/RpcSharedStream.cs:110-217.
     */
    /* eslint-disable @typescript-eslint/no-unnecessary-condition -- _ended changes across awaits */
    private async _runNonRealTime(
        iterator: AsyncIterator<T>,
        state: _PumpState,
    ): Promise<void> {
        const buffer = new RingBuffer<_StreamItem<T>>(this.ackAdvance + 1);
        let bufferStart = 0;
        let isFullyBuffered = false;

        while (true) {
            // ---- nextAck ----
            // 1. Await for an acknowledgement & process accumulated ACKs.
            let ack = this._tryProcessAcks();
            if (!ack) {
                await this._waitAckReady();
                if (this._ended) return;
                ack = this._tryProcessAcks();
                if (!ack) {
                    // Should not happen (the wait resolved, so at least one ACK
                    // was queued) — defensive exit.
                    return;
                }
            }
            this._lastAckedIndex = ack.nextIndex;

            // 2. Remove what's useless from the buffer.
            const shift = _clamp(ack.nextIndex - bufferStart, 0, buffer.count);
            if (shift > 0) {
                buffer.moveHead(shift);
                bufferStart += shift;
            }

            // 3. Recalculate the next range to send.
            if (this._nextIndex < bufferStart) {
                // The requested item is below the buffer's current head — the
                // source isn't replayable, so fail the stream.
                if (!this._ended)
                    this.sendEnd(new Error('Stream position unavailable.'));
                return;
            }
            let bufferIndex = this._nextIndex - bufferStart;

            // 3. Send as much as we can.
            //
            // .NET separates this into 3.1 (buffer items until the source
            // stalls, using a synchronous "is ready" check) and 3.2 (drain
            // the buffer). TS cannot check if a Promise has resolved without
            // awaiting it, so we collapse both into a single pull-one /
            // send-one loop — which is equivalent when there's no Batcher
            // (the TS sender already sends one item at a time).
            const maxIndex = ack.nextIndex + this.ackAdvance;
            while (this._nextIndex < maxIndex) {
                if (this._ended) return;

                // 3.a. Drain buffered items first.
                if (bufferIndex < buffer.count) {
                    const item = buffer.get(bufferIndex++);
                    if (item.kind === 'value') {
                        this.sendItem(item.value);
                    } else if (item.kind === 'end') {
                        if (!this._ended) this.sendEnd();
                        return;
                    } else {
                        if (!this._ended) this.sendEnd(item.error);
                        return;
                    }
                    continue;
                }

                // 3.b. Buffer exhausted.
                if (isFullyBuffered) break;  // goto nextAck
                if (!buffer.hasRemainingCapacity) break;  // goto nextAck

                // 3.c. Pull one more item from the source.
                let pulled: _StreamItem<T>;
                try {
                    const r = await iterator.next();
                    if (this._ended) return;
                    if (r.done) {
                        pulled = _endItem;
                        isFullyBuffered = true;
                        state.iteratorDone = true;
                    } else {
                        pulled = { kind: 'value', value: r.value };
                    }
                } catch (e) {
                    state.iteratorDone = true;
                    isFullyBuffered = true;
                    pulled = {
                        kind: 'error',
                        error: e instanceof Error ? e : new Error(String(e)),
                    };
                }
                buffer.pushTail(pulled);
            }
        }
    }

    /**
     * Real-time pump — drops stale items to stay close to the source's head.
     *
     * Direct port of .NET `OnRunRealTime` at
     * src/ActualLab.Rpc/Infrastructure/RpcSharedStream.cs:219-420.
     */
    private async _runRealTime(
        iterator: AsyncIterator<T>,
        state: _PumpState,
    ): Promise<void> {
        const buffer = new RingBuffer<_StreamItem<T>>(this.ackAdvance + 1);
        let bufferStart = 0;
        let isFullyBuffered = false;

        while (true) {
            // ---- nextAck ----
            // 1. Await for an acknowledgement & process accumulated ACKs.
            let ack = this._tryProcessAcks();
            if (!ack) {
                await this._waitAckReady();
                if (this._ended) return;
                ack = this._tryProcessAcks();
                if (!ack) return;
            }
            this._lastAckedIndex = ack.nextIndex;

            // 1.1. Reconnect: clear stale buffer and skip to next canSkipTo.
            if (ack.mustReset && !isFullyBuffered) {
                buffer.clear();
                bufferStart = this._nextIndex;
                // Drain the source until we find an item that canSkipTo accepts,
                // or the source ends / throws.
                while (true) {
                    if (this._ended) return;
                    let item: _StreamItem<T>;
                    let accepted = false;
                    try {
                        const r = await iterator.next();
                        if (this._ended) return;
                        if (r.done) {
                            item = _endItem;
                            isFullyBuffered = true;
                            state.iteratorDone = true;
                        } else if (this.canSkipTo(r.value)) {
                            item = { kind: 'value', value: r.value };
                            accepted = true;
                        } else {
                            continue; // discard; keep draining
                        }
                    } catch (e) {
                        state.iteratorDone = true;
                        isFullyBuffered = true;
                        item = {
                            kind: 'error',
                            error: e instanceof Error ? e : new Error(String(e)),
                        };
                    }
                    buffer.pushTail(item);
                    if (accepted || isFullyBuffered) break;
                }
            }

            // 2. Remove what's useless from the buffer.
            const shift = _clamp(ack.nextIndex - bufferStart, 0, buffer.count);
            if (shift > 0) {
                buffer.moveHead(shift);
                bufferStart += shift;
            }

            // 3. Recalculate the next range to send.
            if (this._nextIndex < bufferStart) {
                if (!this._ended)
                    this.sendEnd(new Error('Stream position unavailable.'));
                return;
            }
            let bufferIndex = this._nextIndex - bufferStart;

            // 3. Send as much as we can (pull-one / send-one; see the
            //    equivalent comment in _runNonRealTime).
            const maxIndex = ack.nextIndex + this.ackAdvance;
            while (this._nextIndex < maxIndex) {
                if (this._ended) return;
                // Bail out so step 1 can process a mustReset ACK immediately.
                if (this._acks.length > 0) break;

                if (bufferIndex < buffer.count) {
                    const item = buffer.get(bufferIndex++);
                    if (item.kind === 'value') {
                        this.sendItem(item.value);
                    } else if (item.kind === 'end') {
                        if (!this._ended) this.sendEnd();
                        return;
                    } else {
                        if (!this._ended) this.sendEnd(item.error);
                        return;
                    }
                    continue;
                }
                if (isFullyBuffered) break;  // goto nextAck
                if (!buffer.hasRemainingCapacity) break;

                let pulled: _StreamItem<T>;
                try {
                    const r = await iterator.next();
                    if (this._ended) return;
                    if (r.done) {
                        pulled = _endItem;
                        isFullyBuffered = true;
                        state.iteratorDone = true;
                    } else {
                        pulled = { kind: 'value', value: r.value };
                    }
                } catch (e) {
                    state.iteratorDone = true;
                    isFullyBuffered = true;
                    pulled = {
                        kind: 'error',
                        error: e instanceof Error ? e : new Error(String(e)),
                    };
                }
                buffer.pushTail(pulled);
            }

            // 4. Ceiling hit (but source not exhausted): drain source while
            //    no ACK is queued, keeping ONLY the latest canSkipTo item
            //    seen during the drain. Once an ACK arrives (or source ends)
            //    stash that latest item into the buffer and loop back to
            //    process the ACK.
            //
            //    Deviation from .NET (RpcSharedStream.cs:366-418), which
            //    breaks after the FIRST canSkipTo it sees. The TS version
            //    stays closer to the consumer by continuing to consume stale
            //    items and keeping only the freshest — matching the semantic
            //    that the existing `rpc-stream-realtime.test.ts` suite
            //    verifies (real-time streams should aggressively drop stale
            //    frames to stay current).
            //
            //    Implementation note: this loop deliberately does NOT race
            //    `iterator.next()` against an ACK promise — that pattern
            //    (a) loses one item on every ACK arrival because the
            //    abandoned source-pull promise still advances the iterator,
            //    and (b) creates two promises per iteration, which on
            //    Windows's coarser microtask scheduling is slow enough to
            //    miss the test's deadline. Instead we await each source
            //    pull, then check the ACK queue. JS is single-threaded, so
            //    any onAck() invoked by inbound network events runs in the
            //    microtask gap between iterations and is observable on the
            //    next `_acks.length` check.
            if (this._nextIndex >= maxIndex && !isFullyBuffered && !this._ended) {
                let latest: _StreamItem<T> | null = null;
                drain: while (!this._ended) {
                    if (this._acks.length > 0) break drain;
                    let r: IteratorResult<T>;
                    try {
                        r = await iterator.next();
                    } catch (e) {
                        state.iteratorDone = true;
                        isFullyBuffered = true;
                        latest = {
                            kind: 'error',
                            error: e instanceof Error ? e : new Error(String(e)),
                        };
                        break drain;
                    }
                    if (this._ended) return;
                    if (r.done) {
                        latest = _endItem;
                        isFullyBuffered = true;
                        state.iteratorDone = true;
                        break drain;
                    }
                    if (this.canSkipTo(r.value)) {
                        latest = { kind: 'value', value: r.value };
                    }
                    // else: discard, keep draining.
                }
                if (latest !== null) {
                    const wasFull = buffer.isFull;
                    buffer.pushTailAndMoveHeadIfFull(latest);
                    if (wasFull) bufferStart++;
                }
                // Go back to nextAck (outer loop) to flush/send what we buffered.
            }
        }
    }
    /* eslint-enable @typescript-eslint/no-unnecessary-condition */

    /** Drain all queued ACKs, returning the most recent one (or null if none). */
    private _tryProcessAcks(): { nextIndex: number; mustReset: boolean } | null {
        if (this._acks.length === 0) return null;
        let last: { nextIndex: number; mustReset: boolean } | null = null;
        while (this._acks.length > 0) {
            const a = this._acks.shift()!;
            last = a;
            if (a.mustReset || this._nextIndex < a.nextIndex) {
                this._nextIndex = a.nextIndex;
            }
        }
        return last;
    }

    /** Wait for the ACK queue to have at least one entry (or `_ended`). */
    private async _waitAckReady(): Promise<void> {
        if (this._acks.length > 0 || this._ended) return;
        this._whenAckReady = new PromiseSource<void>();
        await this._whenAckReady.promise;
        this._whenAckReady = null;
    }

    // -- IRpcObject --

    reconnect(): void {
        // Server-side streams don't support reconnection — the client
        // creates a new stream on reconnect.
    }

    disconnect(): void {
        if (this._ended) return;
        this._ended = true;
        this._abortController.abort();
        if (!this._started.isCompleted) {
            this._started.resolve();
        }
        if (this._whenAckReady) {
            this._whenAckReady.resolve();
            this._whenAckReady = null;
        }
        this.peer.sharedObjects.unregister(this);

        // Iterator force-close policy:
        //  - Plain AsyncIterable: abortSignal is unobserved, so force-close now.
        //  - Factory source (abortSignal-aware): give it a grace period to exit
        //    gracefully, then force-close if still pumping.
        const iterator = this._iterator;
        if (!iterator) return;
        if (!this.sourceUsesAbortSignal) {
            this._iterator = null;
            this._forceCloseIterator(iterator);
            return;
        }
        setTimeout(() => {
            if (this._iterator === iterator) {
                this._iterator = null;
                this._forceCloseIterator(iterator);
            }
        }, RpcStream.disconnectGracePeriodMs);
    }

    private _forceCloseIterator(iterator: AsyncIterator<T>): void {
        try {
            const ret = iterator.return?.(undefined);
            if (ret) void ret.catch(() => { /* ignore */ });
        } catch { /* ignore */ }
    }
}

// -- Internal helpers (file-local) --

/** Wrapper matching .NET's `Result<T>` — value, end-of-stream, or error. */
type _StreamItem<T> =
    | { kind: 'value'; value: T }
    | { kind: 'end' }
    | { kind: 'error'; error: Error };

/** Singleton "source exhausted" marker (matches .NET's `NoMoreItemsTag`). */
const _endItem: _StreamItem<never> = { kind: 'end' };

/** Shared mutable state between writeFrom and its per-mode pump. */
interface _PumpState {
    iteratorDone: boolean;
}

function _clamp(value: number, min: number, max: number): number {
    return value < min ? min : value > max ? max : value;
}
