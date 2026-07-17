import { PromiseSource, RingBuffer } from '@actuallab/core';
import { getLogs } from './logging.js';
import { toExceptionInfo } from './rpc-error.js';
import type { RpcObjectId, IRpcObject } from './rpc-object.js';
import { RpcObjectKind } from './rpc-object.js';
import type { RpcPeer } from './rpc-peer.js';
import { RpcStream } from './rpc-stream.js';

const { warnLog } = getLogs('RpcSharedStream');

/** Default ack period for server-side streams. */
const DEFAULT_ACK_PERIOD = 256;
/** Default ack advance window for server-side streams. */
const DEFAULT_ACK_ADVANCE = 128;
/** Sliding window over which minRttMs is the minimum of ack round-trip samples. */
const RTT_WINDOW_MS = 20_000;

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
 * The sender uses a single ACK-driven loop:
 *  - On each ACK, drain the source synchronously into the ring buffer up to
 *    `effectiveBufferSize` (this lets us pre-buffer past `ackAdvance`).
 *  - For real-time, optionally compact the unsent suffix to the latest
 *    buffered `canSkipTo` item.
 *  - Send items up to the `ackAdvance` flow-control window.
 *  - Wait for the next ACK.
 *
 * Setting `bufferSize > ackAdvance` lets the sender pre-buffer items past
 * the in-flight window so a fresh ACK can be served from RAM instead of
 * waiting on the source.
 *
 * Direct port of .NET `RpcSharedStream<T>` at
 * src/ActualLab.Rpc/Infrastructure/RpcSharedStream.cs.
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
    /**
     * Wire-level flow-control window: the maximum index the sender may
     * advance past the most recently acknowledged index. Mirrors .NET
     * `RpcStream.AckAdvance`.
     */
    readonly ackAdvance: number;
    /**
     * Effective local ring buffer capacity used by the pump. Set larger
     * than `ackAdvance` to pre-buffer items beyond the in-flight window;
     * values below `ackAdvance` are clamped up (with a warning) at
     * construction time. Not transmitted on the wire.
     */
    readonly bufferSize: number;
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

    // -- RTT sampling (send → matching ack round trips, windowed minimum) --
    private _pendingSendTimes: { index: number; sentAtMs: number }[] = [];
    private _rttSamples: { atMs: number; rttMs: number }[] = [];

    // -- Recorder-controller metrics (Step 9.1) --
    /** Total items skipped via real-time `canSkipTo` compaction. */
    private _skipCount = 0;
    /** Fires after a $sys.Ack has been processed (post-compaction). The
     *  recorder's quality controller uses this to bump a "last ACK at"
     *  watchdog so it can distinguish "stuck" (no ACK > N s) from
     *  "throttled" (ACKs flowing). */
    onAckProcessed?: () => void;
    /** Fires after each item is pushed onto the local ring buffer. The
     *  argument is the buffer count *after* the push. Together with
     *  `onAckProcessed` (which is the only place items leave the buffer),
     *  this gives controllers a complete picture of buffer utilization.
     *  When `bufferedCount === bufferSize`, the source pull is paused
     *  until the next ACK frees space. Listener errors are swallowed. */
    onBuffered?: (bufferedCount: number) => void;

    get nextIndex(): number {
        return this._nextIndex;
    }
    get lastAckIndex(): number {
        return this._lastAckedIndex;
    }
    get skipCount(): number {
        return this._skipCount;
    }
    // Windowed-MIN ack round trip (ms, -1 until sampled): converges on propagation
    // RTT — unlike a mean/EMA it is not inflated by self-induced queuing.
    get minRttMs(): number {
        const cutoffMs = Date.now() - RTT_WINDOW_MS;
        while (this._rttSamples.length > 0 && this._rttSamples[0].atMs < cutoffMs)
            this._rttSamples.shift();
        if (this._rttSamples.length === 0)
            return -1;

        let min = this._rttSamples[0].rttMs;
        for (const s of this._rttSamples) {
            if (s.rttMs < min)
                min = s.rttMs;
        }
        return min;
    }

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
        bufferSize?: number,
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

        if (bufferSize !== undefined && bufferSize !== 0 && bufferSize < ackAdvance)
            warnLog?.log(
                `RpcStream bufferSize (${bufferSize}) is below ackAdvance (${ackAdvance}); using ackAdvance as buffer size.`,
            );
        this.bufferSize = bufferSize !== undefined && bufferSize >= ackAdvance ? bufferSize : ackAdvance;
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

        // Host mismatch — the consumer reconnected to a different server
        // instance, so our stream can never satisfy its acks (RpcSharedStream.cs:82-86).
        if (mustReset && hostId !== this.id.hostId) {
            this._sendDisconnect();
            return;
        }

        if (!this._started.isCompleted) {
            // A not-yet-started stream only accepts the initial connect ack.
            if (mustReset && nextIndex === 0)
                this._started.resolve();
            else {
                this._sendDisconnect();
                return;
            }
        } else if (this._ended) {
            // Ack for an already-completed stream — nothing left to serve.
            this._sendDisconnect();
            return;
        } else if (mustReset && !this.allowReconnect) {
            // Reset (reconnect) ack on a non-reconnectable stream — reject.
            this._sendDisconnect();
            return;
        }

        this._sampleRtt(nextIndex, mustReset);
        this._acks.push({ nextIndex, mustReset });
        if (this._whenAckReady) {
            this._whenAckReady.resolve();
            this._whenAckReady = null;
        }
    }

    // Tells the consumer this shared stream is gone, then disposes it —
    // mirrors RpcSharedStream.SendDisconnect (RpcSharedStream.cs:284-289).
    private _sendDisconnect(): void {
        const conn = this.peer.connection;
        if (conn)
            this.peer.hub.systemCallSender.disconnect(conn, this.peer.serializationFormat, [this.id.localId]);

        this.disconnect();
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
                conn, this.peer.serializationFormat, this.id.localId, this._nextIndex, item,
            );
            this._pendingSendTimes.push({ index: this._nextIndex, sentAtMs: Date.now() });
        }
        this._nextIndex++;
    }

    /** Send a batch of items to the client. See {@link sendItem} for disconnect semantics. */
    sendBatch(items: T[]): void {
        if (this._ended || items.length === 0) return;
        const conn = this.peer.connection;
        if (conn) {
            this.peer.hub.systemCallSender.batch(
                conn, this.peer.serializationFormat, this.id.localId, this._nextIndex, items,
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
            ? toExceptionInfo(error)
            : { TypeRef: '', Message: '' };
        this.peer.hub.systemCallSender.end(
            conn, this.peer.serializationFormat, this.id.localId, this._nextIndex, errorInfo,
        );
    }

    /**
     * Consume an AsyncIterable and send all items to the client.
     *
     * Matches .NET `RpcSharedStream<T>.OnRun` at
     * src/ActualLab.Rpc/Infrastructure/RpcSharedStream.cs.
     */
    async writeFrom(source: AsyncIterable<T>): Promise<void> {
        await this._started;
        if (this._ended) return;

        const iterator = source[Symbol.asyncIterator]();
        this._iterator = iterator;
        // Tracks whether the generator has reached a terminal state (done=true
        // or thrown). When true, its finally block has already run and the
        // iterator reference can be cleared. When false, disconnect()'s
        // scheduled iterator.return() is what drives it to its finally.
        const state: _PumpState = { iteratorDone: false };
        try {
            await this._run(iterator, state);
        } finally {
            if (state.iteratorDone && this._iterator === iterator) {
                this._iterator = null;
            }
        }
    }

    /**
     * ACK-driven pump. Non-real-time streams apply normal backpressure at
     * `ackAdvance`; real-time streams may compact the already-buffered unsent
     * suffix to the latest buffered `canSkipTo` item after the consumer ACKs.
     * With `bufferSize > ackAdvance` the local ring buffer holds more than the
     * in-flight window — real-time mode uses the extra space to pre-buffer.
     */
    /* eslint-disable @typescript-eslint/no-unnecessary-condition -- _ended changes across awaits */
    private async _run(
        iterator: AsyncIterator<T>,
        state: _PumpState,
    ): Promise<void> {
        const buffer = new RingBuffer<_StreamItem<T>>(this.bufferSize + 1);
        const isRealTime = this.isRealTime;
        let bufferStart = 0;
        let isFullyBuffered = false;
        let whenMovedNext: Promise<IteratorResult<T>> | null = null;
        const pending = Symbol('pending');

        const readNext = async (): Promise<IteratorResult<T>> => {
            const next = whenMovedNext ?? iterator.next();
            whenMovedNext = null;
            return await next;
        };

        const tryReadReady = async (): Promise<IteratorResult<T> | typeof pending> => {
            whenMovedNext ??= iterator.next();
            return await Promise.race([
                whenMovedNext,
                Promise.resolve(pending),
            ]);
        };

        const fireBuffered = (): void => {
            try {
                this.onBuffered?.(buffer.count);
            } catch { /* listener errors don't break the pump */ }
        };

        const bufferNext = async (onlyIfReady: boolean, prefetchNext: boolean): Promise<boolean> => {
            let r: IteratorResult<T> | typeof pending;
            try {
                r = onlyIfReady ? await tryReadReady() : await readNext();
                if (r === pending)
                    return false;
                whenMovedNext = null;
                if (this._ended) return false;
                if (r.done) {
                    buffer.pushTail(_endItem);
                    isFullyBuffered = true;
                    state.iteratorDone = true;
                } else {
                    buffer.pushTail({ kind: 'value', value: r.value });
                    if (prefetchNext)
                        whenMovedNext = iterator.next();
                }
                fireBuffered();
                return true;
            } catch (e) {
                whenMovedNext = null;
                state.iteratorDone = true;
                isFullyBuffered = true;
                buffer.pushTail({
                    kind: 'error',
                    error: e instanceof Error ? e : new Error(String(e)),
                });
                fireBuffered();
                return true;
            }
        };

        while (true) {
            // ---- nextAck ----
            // 1. Await for an acknowledgement & process accumulated ACKs.
            let ack = this._tryProcessAcks();
            if (!ack) {
                await this._waitAckReady();
                if (this._ended) return;
                ack = this._tryProcessAcks();
                if (!ack) {
                    warnLog?.log("Something is off: couldn't read an acknowledgement");
                    return;
                }
            }
            this._lastAckedIndex = ack.nextIndex;

            // 1.1. Reconnect: real-time streams can clear stale buffered data
            // and restart from the next source item accepted by canSkipTo.
            if (isRealTime && ack.mustReset && !isFullyBuffered) {
                buffer.clear();
                bufferStart = this._nextIndex;
                while (true) {
                    if (this._ended) return;
                    let item: _StreamItem<T>;
                    let accepted = false;
                    try {
                        const r = await readNext();
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
                    fireBuffered();
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

            // 3. Send as much as the current ACK window allows.
            const maxIndex = ack.nextIndex + this.ackAdvance;
            while (this._nextIndex < maxIndex) {
                if (this._ended) return;

                if (isRealTime) {
                    // Real-time: pre-buffer aggressively up to the ring's
                    // capacity (which may exceed ackAdvance when bufferSize
                    // is set), so a freshly arrived ACK is served from RAM.
                    while (buffer.hasRemainingCapacity && !isFullyBuffered && this._acks.length === 0) {
                        if (!await bufferNext(true, true))
                            break;
                    }
                    if (bufferIndex >= buffer.count
                        && !isFullyBuffered
                        && buffer.hasRemainingCapacity
                        && this._acks.length === 0) {
                        await bufferNext(false, true);
                    }
                    if (ack.nextIndex > 0) {
                        const result = _compactBufferedUnsentSuffix(
                            buffer, bufferIndex, this.canSkipTo);
                        bufferIndex = result.newIndex;
                        if (result.skipped > 0)
                            this._skipCount += result.skipped;
                    }

                    // Bail out so step 1 can process a mustReset ACK immediately.
                    if (this._acks.length > 0) break;
                }

                // Drain buffered items until the window is full.
                while (this._nextIndex < maxIndex && bufferIndex < buffer.count) {
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
                }
                if (this._nextIndex >= maxIndex)
                    break;
                if (isFullyBuffered) break;
                if (!buffer.hasRemainingCapacity) break;

                if (!isRealTime)
                    await bufferNext(false, false);
            }
        }
    }
    /* eslint-enable @typescript-eslint/no-unnecessary-condition */

    /** Drain all queued ACKs, returning the most recent one (or null if none).
     *  A reconnect's reset ACK may be drained in one batch with regular
     *  consumption ACKs the client sends right after it, so `mustReset` is
     *  OR-ed over the whole batch — the last ACK alone can't represent it. */
    private _tryProcessAcks(): { nextIndex: number; mustReset: boolean } | null {
        if (this._acks.length === 0) return null;
        let nextIndex = 0;
        let mustReset = false;
        while (this._acks.length > 0) {
            const a = this._acks.shift()!;
            nextIndex = a.nextIndex;
            mustReset ||= a.mustReset;
            if (a.mustReset || this._nextIndex < a.nextIndex) {
                this._nextIndex = a.nextIndex;
            }
        }
        try {
            this.onAckProcessed?.();
        } catch { /* listener errors don't break the pump */ }
        return { nextIndex, mustReset };
    }

    /** Wait for the ACK queue to have at least one entry (or `_ended`). */
    private async _waitAckReady(): Promise<void> {
        if (this._acks.length > 0 || this._ended) return;
        this._whenAckReady = new PromiseSource<void>();
        await this._whenAckReady;
        this._whenAckReady = null;
    }

    // One RTT sample per ack: time from sending the newest acked item to the
    // ack's arrival (a slight upper bound — includes the server's ack cadence).
    private _sampleRtt(ackNextIndex: number, mustReset: boolean): void {
        if (mustReset) {
            this._pendingSendTimes.length = 0;
            return;
        }

        let sentAtMs = -1;
        while (this._pendingSendTimes.length > 0 && this._pendingSendTimes[0].index < ackNextIndex) {
            sentAtMs = this._pendingSendTimes[0].sentAtMs;
            this._pendingSendTimes.shift();
        }
        if (sentAtMs < 0)
            return;

        const nowMs = Date.now();
        const cutoffMs = nowMs - RTT_WINDOW_MS;
        while (this._rttSamples.length > 0 && this._rttSamples[0].atMs < cutoffMs)
            this._rttSamples.shift();
        this._rttSamples.push({ atMs: nowMs, rttMs: nowMs - sentAtMs });
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

function _compactBufferedUnsentSuffix<T>(
    buffer: RingBuffer<_StreamItem<T>>,
    firstUnsentIndex: number,
    canSkipTo: (item: T) => boolean,
): { newIndex: number; skipped: number } {
    // Keep items that have already been assigned RPC stream indexes, then
    // collapse the unsent suffix to the latest buffered restart point. The
    // surviving suffix is intentionally sent under fresh RPC stream indexes;
    // item-level timestamps/frame indexes are the caller's responsibility.
    let skipToIndex = -1;
    for (let i = firstUnsentIndex; i < buffer.count; i++) {
        const item = buffer.get(i);
        if (item.kind === 'value' && canSkipTo(item.value))
            skipToIndex = i;
    }
    if (skipToIndex <= firstUnsentIndex)
        return { newIndex: firstUnsentIndex, skipped: 0 };

    const cutFrom = firstUnsentIndex;
    const cutTo = skipToIndex;

    const items = buffer.toArray();
    buffer.clear();
    for (let i = 0; i < cutFrom; i++)
        buffer.pushTail(items[i]);
    for (let i = cutTo; i < items.length; i++)
        buffer.pushTail(items[i]);
    return { newIndex: firstUnsentIndex, skipped: skipToIndex - firstUnsentIndex };
}
