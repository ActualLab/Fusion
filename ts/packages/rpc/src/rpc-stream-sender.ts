import { PromiseSource, RingBuffer } from '@actuallab/core';
import { getLogs } from './logging.js';
import type { RpcObjectId, IRpcObject } from './rpc-object.js';
import { RpcObjectKind } from './rpc-object.js';
import type { RpcPeer } from './rpc-peer.js';
import { RpcStream } from './rpc-stream.js';

const { warnLog } = getLogs('RpcSharedStream');

/** Default ack period for server-side streams. */
const DEFAULT_ACK_PERIOD = 256;
/** Default send buffer size for server-side streams. */
const DEFAULT_BUFFER_SIZE = 128;

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
 * to `ack.nextIndex + bufferSize`. While the peer is disconnected no ACKs
 * arrive, so the source is never pulled — the sender naturally pauses
 * instead of spinning.
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

    // -- Recorder-controller metrics (Step 9.1) --
    /** Total items skipped via real-time `canSkipTo` compaction. */
    private _skipCount = 0;
    /** Fires after a $sys.Ack has been processed (post-compaction). The
     *  recorder's quality controller uses this to bump a "last ACK at"
     *  watchdog so it can distinguish "stuck" (no ACK > N s) from
     *  "throttled" (ACKs flowing). */
    onAckProcessed?: () => void;

    get nextIndex(): number {
        return this._nextIndex;
    }
    get lastAckIndex(): number {
        return this._lastAckedIndex;
    }
    get skipCount(): number {
        return this._skipCount;
    }

    // -- ACK queue (analog of .NET's Channel<(long, bool)>) --
    private _acks: { nextIndex: number; mustReset: boolean }[] = [];
    private _whenAckReady: PromiseSource<void> | null = null;

    constructor(
        peer: RpcPeer,
        ackPeriod = DEFAULT_ACK_PERIOD,
        bufferSize = DEFAULT_BUFFER_SIZE,
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
        this.bufferSize = bufferSize;
        this.sourceUsesAbortSignal = sourceUsesAbortSignal;
    }

    /** AbortSignal that is aborted when the sender is disconnected. */
    get abortSignal(): AbortSignal {
        return this._abortController.signal;
    }

    /** Returns the stream reference string for the $sys.Ok response. */
    toRef(): string {
        return `${this.id.hostId},${this.id.localId},${this.ackPeriod},${this.bufferSize},${this.allowReconnect ? '1' : '0'},${this.isRealTime ? '1' : '0'}`;
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
                conn, this.peer.serializationFormat, this.id.localId, this._nextIndex, item,
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
            ? { TypeRef: 'System.Exception', Message: error.message }
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
            await this._run(iterator, state);
        } finally {
            if (state.iteratorDone && this._iterator === iterator) {
                this._iterator = null;
            }
        }
    }

    /**
     * ACK-driven pump. Non-real-time streams apply normal backpressure at
     * `bufferSize`; real-time streams may compact the already-buffered unsent
     * suffix to the latest buffered `canSkipTo` item after the consumer ACKs.
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
                return true;
            } catch (e) {
                whenMovedNext = null;
                state.iteratorDone = true;
                isFullyBuffered = true;
                buffer.pushTail({
                    kind: 'error',
                    error: e instanceof Error ? e : new Error(String(e)),
                });
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
                    // Mirrors RpcSharedStream.cs:252.
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
                // Drain the source until we find an item that canSkipTo accepts,
                // or the source ends / throws.
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
            const maxIndex = ack.nextIndex + this.bufferSize;
            while (this._nextIndex < maxIndex) {
                if (this._ended) return;

                if (isRealTime) {
                    // Real-time compaction only uses the already-buffered
                    // unsent suffix; it doesn't pull past the buffer just to
                    // discover a future skip target.
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
                if (isFullyBuffered) break;  // goto nextAck
                if (!buffer.hasRemainingCapacity) break;

                if (!isRealTime)
                    await bufferNext(false, false);
            }

            // 4. Ceiling hit: wait for ACK. Real-time streams compact only the
            // already-buffered unsent suffix; they don't pull ahead just to
            // discover a future skip target.
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
        try {
            this.onAckProcessed?.();
        } catch { /* listener errors don't break the pump */ }
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
