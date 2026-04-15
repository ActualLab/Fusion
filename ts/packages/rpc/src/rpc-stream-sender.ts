import { PromiseSource } from '@actuallab/core';
import type { RpcObjectId, IRpcObject } from './rpc-object.js';
import { RpcObjectKind } from './rpc-object.js';
import type { RpcPeer } from './rpc-peer.js';

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

    private _nextIndex = 0;
    private _ended = false;
    private _started = new PromiseSource<void>();
    private _lastAckedIndex = 0;
    private _ackWaiting: PromiseSource<void> | null = null;
    private _resetRequested = false;

    constructor(
        peer: RpcPeer,
        ackPeriod = DEFAULT_ACK_PERIOD,
        ackAdvance = DEFAULT_ACK_ADVANCE,
        allowReconnect = true,
        isRealTime = false,
        canSkipTo: (item: T) => boolean = () => true,
    ) {
        const localId = peer.sharedObjects.nextId();
        this.id = { hostId: peer.hub.hubId, localId };
        this.allowReconnect = allowReconnect;
        this.isRealTime = isRealTime;
        this.canSkipTo = canSkipTo;
        this.peer = peer;
        this.ackPeriod = ackPeriod;
        this.ackAdvance = ackAdvance;
    }

    /** Returns the stream reference string for the $sys.Ok response. */
    toRef(): string {
        return `${this.id.hostId},${this.id.localId},${this.ackPeriod},${this.ackAdvance},${this.allowReconnect ? '1' : '0'},${this.isRealTime ? '1' : '0'}`;
    }

    /** Called by system call handler when $sys.Ack is received from the client. */
    onAck(nextIndex: number, hostId: string): void {
        const mustReset = hostId !== '' && hostId !== RpcStreamSender._emptyGuid;

        if (!this._started.isCompleted) {
            this._started.resolve();
        }

        if (mustReset && this.isRealTime) {
            // Real-time reconnect: reset position and request skip to next canSkipTo item
            this._nextIndex = nextIndex;
            this._lastAckedIndex = nextIndex;
            this._resetRequested = true;
        } else {
            if (mustReset || nextIndex > this._lastAckedIndex) {
                this._lastAckedIndex = nextIndex;
            }
        }

        if (this._ackWaiting) {
            this._ackWaiting.resolve();
            this._ackWaiting = null;
        }
    }

    /** Called by system call handler when $sys.AckEnd is received from the client. */
    onAckEnd(_hostId: string): void {
        this._ended = true;
        this.peer.sharedObjects.unregister(this);
    }

    /** Send a single item to the client. */
    sendItem(item: T): void {
        if (this._ended) return;
        const conn = this.peer.connection;
        if (!conn) return;
        this.peer.hub.systemCallSender.item(
            conn,
            this.peer.format,
            this.id.localId,
            this._nextIndex,
            item
        );
        this._nextIndex++;
    }

    /** Send a batch of items to the client. */
    sendBatch(items: T[]): void {
        if (this._ended || items.length === 0) return;
        const conn = this.peer.connection;
        if (!conn) return;
        this.peer.hub.systemCallSender.batch(
            conn,
            this.peer.format,
            this.id.localId,
            this._nextIndex,
            items
        );
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
            conn,
            this.peer.format,
            this.id.localId,
            this._nextIndex,
            errorInfo
        );
    }

    /**
     * Consume an AsyncIterable and send all items to the client.
     * Waits for the client's initial Ack before starting to pump items.
     *
     * When `isRealTime` is true, applies flow control based on `ackAdvance`:
     * if the sender is `ackAdvance` items ahead of the last ACK, it enters
     * skip mode — draining the source and discarding items until it finds
     * one where `canSkipTo` returns true.
     */
    async writeFrom(source: AsyncIterable<T>): Promise<void> {
        await this._started.promise;

        const iterator = source[Symbol.asyncIterator]();
        try {
            for (;;) {
                const next = await iterator.next();
                 
                if (next.done || this._ended) break;

                const item = next.value;

                // Real-time reconnect: drain source to next canSkipTo item
                if (this._resetRequested) {
                    if (!this.canSkipTo(item)) continue;
                    this._resetRequested = false;
                    this.sendItem(item);
                    continue;
                }

                // Flow control: check if we've hit the AckAdvance ceiling
                if (this._nextIndex < this._lastAckedIndex + this.ackAdvance) {
                    // Under budget — send normally
                    this.sendItem(item);
                    continue;
                }

                if (!this.isRealTime) {
                    // Normal mode: wait for ACK before sending
                    await this._waitForAckBudget();
                    // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- _ended can change during await
                    if (this._ended) return;
                    this.sendItem(item);
                    continue;
                }

                // Real-time mode: hit the ceiling. Drain the source,
                // discarding non-skip-targets, keeping the latest skip target.
                // Stop draining when budget becomes available or source exhausts.
                let latestSkipTarget: T | undefined = this.canSkipTo(item) ? item : undefined;
                let sourceExhausted = false;

                while (this._nextIndex >= this._lastAckedIndex + this.ackAdvance) {
                    // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- _ended can change during await
                    if (this._ended) return;
                    const n = await iterator.next();
                    if (n.done) {
                        sourceExhausted = true;
                        break;
                    }
                    if (this.canSkipTo(n.value)) {
                        latestSkipTarget = n.value;
                    }
                }

                // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- _ended can change during await
                if (this._ended) return;

                if (latestSkipTarget !== undefined) {
                    this.sendItem(latestSkipTarget);
                }

                if (sourceExhausted) {
                    // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- _ended can change during await
                    if (!this._ended) this.sendEnd();
                    return;
                }
            }
             
            if (!this._ended) {
                this.sendEnd();
            }
        } catch (e) {
            if (!this._ended) {
                this.sendEnd(e instanceof Error ? e : new Error(String(e)));
            }
        }
    }

    /** Wait until _nextIndex < _lastAckedIndex + ackAdvance. */
    private async _waitForAckBudget(): Promise<void> {
        while (this._nextIndex >= this._lastAckedIndex + this.ackAdvance) {
            if (this._ended) return;
            this._ackWaiting = new PromiseSource<void>();
            await this._ackWaiting.promise;
        }
    }

    // -- IRpcObject --

    reconnect(): void {
        // Server-side streams don't support reconnection — the client
        // creates a new stream on reconnect.
    }

    disconnect(): void {
        this._ended = true;
        if (!this._started.isCompleted) {
            // Resolve (not reject) — writeFrom() will see _ended=true and exit cleanly.
            // Rejecting would cause unhandled rejections when writeFrom() is void-called.
            this._started.resolve();
        }
        this.peer.sharedObjects.unregister(this);
    }
}
