import Denque from 'denque';
import { PromiseSource } from '@actuallab/core';
import type { RpcObjectId, IRpcObject } from './rpc-object.js';
import { RpcObjectKind } from './rpc-object.js';
import type { RpcPeer } from './rpc-peer.js';

/** Parsed stream reference from the server's result payload. */
export interface RpcStreamRef {
    readonly hostId: string;
    readonly localId: number;
    readonly ackPeriod: number;
    readonly ackAdvance: number;
    readonly allowReconnect: boolean;
    readonly isRealTime: boolean;
}

/**
 * Parses a stream reference string of the form "{hostId},{localId},{ackPeriod},{ackAdvance}".
 * Returns null if the value is not a valid stream reference.
 */
export function parseStreamRef(value: unknown): RpcStreamRef | null {
    // Text format: "hostId,localId,ackPeriod,ackAdvance[,allowReconnect[,isRealTime]]"
    if (typeof value === 'string') {
        const parts = value.split(',');
        if (parts.length < 4 || parts.length > 6) return null;
        const hostId = parts[0]!;
        const localId = parseInt(parts[1]!, 10);
        const ackPeriod = parseInt(parts[2]!, 10);
        const ackAdvance = parseInt(parts[3]!, 10);
        if (isNaN(localId) || isNaN(ackPeriod) || isNaN(ackAdvance))
            return null;
        const allowReconnect = parts.length < 5 || parts[4] !== '0';
        const isRealTime = parts.length >= 6 && parts[5] === '1';
        return { hostId, localId, ackPeriod, ackAdvance, allowReconnect, isRealTime };
    }
    // Binary (MessagePack) format: { SerializedId: [hostId, localId], AckPeriod, AckAdvance, AllowReconnect }
    if (typeof value === 'object' && value !== null) {
        const obj = value as Record<string, unknown>;
        const serializedId = obj.SerializedId as unknown[];
        if (!Array.isArray(serializedId) || serializedId.length < 2)
            return null;
        const rawHostId = serializedId[0];
        const hostId = String(rawHostId);
        const localId = Number(serializedId[1]);
        const ackPeriod = Number(obj.AckPeriod ?? 256);
        const ackAdvance = Number(obj.AckAdvance ?? 128);
        const allowReconnect = obj.AllowReconnect !== false;
        const isRealTime = obj.IsRealTime === true;
        return { hostId, localId, ackPeriod, ackAdvance, allowReconnect, isRealTime };
    }
    return null;
}

/**
 * Client-side RPC stream — consumes items sent by the server via system calls
 * ($sys.I, $sys.B, $sys.End) and implements AsyncIterable<T> for for-await-of consumption.
 *
 * Wire protocol:
 * - Client sends Ack(0, hostId) to start the stream
 * - Server sends I (single item) / B (batch) messages
 * - Client acks every ackPeriod items for flow control
 * - Server sends End to signal completion (with optional error)
 * - Client sends AckEnd to acknowledge completion
 */
export class RpcStream<T> implements AsyncIterable<T>, IRpcObject {
    readonly id: RpcObjectId;
    readonly kind = RpcObjectKind.Remote;
    readonly allowReconnect: boolean;
    readonly peer: RpcPeer;
    readonly ackPeriod: number;
    readonly ackAdvance: number;

    // Double-ended queue so consumption via `shift()` is O(1) and the
    // backing store frees memory as items are drained. The previous
    // `T[]` buffer kept every item for the life of the stream because
    // the iterator tracked a read index instead of shifting — fine for
    // short runs but a slow leak in the browser where a video call can
    // last an hour.
    private _buffer: Denque<T> = new Denque<T>();
    private _nextExpectedIndex = 0;
    private _consumerWaiting: PromiseSource<void> | null = null;
    private _completed = false;
    private _completionError: Error | null = null;
    private _disposed = false;
    private _started = false;
    private _iterating = false;
    private _ackSentUpTo = 0;

    constructor(ref: RpcStreamRef, peer: RpcPeer) {
        this.id = { hostId: ref.hostId, localId: ref.localId };
        this.allowReconnect = ref.allowReconnect;
        this.peer = peer;
        this.ackPeriod = ref.ackPeriod;
        this.ackAdvance = ref.ackAdvance;
    }

    /** Called by system call handler when a single item arrives ($sys.I). */
    onItem(index: number, item: T): void {
        if (this._disposed || this._completed) return;

        if (index > this._nextExpectedIndex) {
            console.warn(
                `[RpcStream] item index gap: localId=${this.id.localId}, expected=${this._nextExpectedIndex}, received=${index}`
            );
            if (!this.allowReconnect) {
                // Sending a reset ack on a non-reconnectable stream only produces a
                // $sys.Disconnect from the server with a generic "Peer disconnected."
                // error. Fail fast with a descriptive error instead so the consumer's
                // retry loop (e.g. VideoPlayer.startPull) can re-pull cleanly.
                this._completed = true;
                this._completionError = new Error(
                    `Stream gap at index ${index} (expected ${this._nextExpectedIndex}); reconnect not allowed`
                );
                this._notifyConsumer();
                return;
            }
            this._sendAck(this._nextExpectedIndex, true);
            return;
        }
        if (index < this._nextExpectedIndex) {
            // Duplicate — ack and ignore
            this._maybeSendAck(index + 1);
            return;
        }

        this._buffer.push(item);
        this._nextExpectedIndex = index + 1;
        this._maybeSendAck(this._nextExpectedIndex);
        this._notifyConsumer();
    }

    /** Called by system call handler when a batch arrives ($sys.B). */
    onBatch(index: number, items: T[]): void {
        if (this._disposed || this._completed) return;

        if (index > this._nextExpectedIndex) {
            console.warn(
                `[RpcStream] batch index gap: localId=${this.id.localId}, expected=${this._nextExpectedIndex}, received=${index}`
            );
            if (!this.allowReconnect) {
                this._completed = true;
                this._completionError = new Error(
                    `Stream gap at index ${index} (expected ${this._nextExpectedIndex}); reconnect not allowed`
                );
                this._notifyConsumer();
                return;
            }
            this._sendAck(this._nextExpectedIndex, true);
            return;
        }
        if (index < this._nextExpectedIndex) {
            this._maybeSendAck(index + items.length);
            return;
        }

        for (const item of items) this._buffer.push(item);
        this._nextExpectedIndex = index + items.length;
        this._maybeSendAck(this._nextExpectedIndex);
        this._notifyConsumer();
    }

    /** Called by system call handler when the stream ends ($sys.End). */
    onEnd(index: number, error: Error | null): void {
        if (this._disposed || this._completed) return;
        this._completed = true;
        this._completionError = error;
        this._notifyConsumer();
    }

    // -- IRpcObject --

    reconnect(): void {
        if (this.allowReconnect) {
            // Re-request from where we left off
            this._sendAck(this._nextExpectedIndex, true);
        } else {
            this.disconnect();
        }
    }

    disconnect(): void {
        if (this._completed) return;
        this._completed = true;
        this._completionError = new Error('Peer disconnected.');
        this._notifyConsumer();
    }

    // -- AsyncIterable --

    [Symbol.asyncIterator](): AsyncIterator<T> {
        if (this._iterating)
            throw new Error('RpcStream can only be iterated once.');
        this._iterating = true;

        const self = this;

        return {
            async next(): Promise<IteratorResult<T>> {
                // Lazy start: send initial ack on first next() call
                if (!self._started) {
                    self._started = true;
                    self._sendAck(0, true);
                }

                // Read from buffer or wait for new data
                while (true) {
                    if (!self._buffer.isEmpty()) {
                        // shift() is O(1) on Denque and releases the slot so the
                        // ring buffer can be reclaimed as the consumer drains.
                        const value = self._buffer.shift()!;
                        return { value, done: false };
                    }

                    if (self._completed) {
                        if (self._completionError) throw self._completionError;
                        return { value: undefined as any, done: true };
                    }

                    // Wait for more data
                    self._consumerWaiting = new PromiseSource<void>();
                    await self._consumerWaiting.promise;
                }
            },

            async return(): Promise<IteratorResult<T>> {
                self._sendAckEnd();
                self.dispose();
                return { value: undefined as any, done: true };
            },
        };
    }

    dispose(): void {
        if (this._disposed) return;
        this._disposed = true;
        this._completed = true;
        this.peer.remoteObjects.unregister(this);
        this._notifyConsumer();
    }

    // -- Private --

    private _notifyConsumer(): void {
        if (this._consumerWaiting !== null) {
            this._consumerWaiting.resolve();
            this._consumerWaiting = null;
        }
    }

    private _maybeSendAck(nextIndex: number): void {
        if (this.ackPeriod <= 0) return;
        // Send ack when we've consumed enough items since the last ack
        const threshold = this._ackSentUpTo + this.ackPeriod;
        if (nextIndex >= threshold) {
            this._sendAck(nextIndex, false);
        }
    }

    private static readonly _emptyGuid = '00000000-0000-0000-0000-000000000000';

    private _sendAck(nextIndex: number, mustReset: boolean): void {
        this._ackSentUpTo = nextIndex;
        const conn = this.peer.connection;
        if (conn) {
            // .NET server treats any non-default hostId as a reset/reconnect request
            // (RpcSharedStream.OnAck: `var mustReset = hostId != default;`).  When the
            // stream has AllowReconnect=false, a reset-flagged ack on an already-running
            // stream is rejected with $sys.Disconnect.  Mirror the .NET client behavior
            // and send the empty Guid for normal progress acks.
            const hostId = mustReset ? this.id.hostId : RpcStream._emptyGuid;
            this.peer.hub.systemCallSender.ack(
                conn,
                this.peer.format,
                this.id.localId,
                nextIndex,
                hostId
            );
        }
    }

    private _sendAckEnd(): void {
        const conn = this.peer.connection;
        if (conn) {
            this.peer.hub.systemCallSender.ackEnd(
                conn,
                this.peer.format,
                this.id.localId,
                this.id.hostId
            );
        }
    }
}

/**
 * Recursively walks a deserialized value and replaces any stream ref strings
 * with live RpcStream<unknown> instances registered on the given peer.
 * This handles nested stream refs inside complex return types (e.g. Table<T>
 * containing RpcStream<Row<T>> fields serialized as "{hostId},{localId},{ackPeriod},{ackAdvance}").
 */
export function resolveStreamRefs(value: unknown, peer: RpcPeer): unknown {
    if (value === null || value === undefined) return value;

    if (typeof value === 'string') {
        const ref = parseStreamRef(value);
        if (ref !== null) {
            const stream = new RpcStream(ref, peer);
            peer.remoteObjects.register(stream);
            return stream;
        }
        return value;
    }

    if (Array.isArray(value)) {
        for (let i = 0; i < value.length; i++) {
            value[i] = resolveStreamRefs(value[i], peer);
        }
        return value;
    }

    if (typeof value === 'object') {
        // Short-circuit binary payloads. TypedArray views (Uint8Array, etc.)
        // and raw ArrayBuffers are "objects" to `typeof`, but Object.keys()
        // returns every numeric index as a "key" — so naive recursion here is
        // O(byteLength) per frame. Binary views can never
        // contain nested stream-ref strings by construction; skip them.
        if (ArrayBuffer.isView(value) || value instanceof ArrayBuffer)
            return value;

        for (const key of Object.keys(value as Record<string, unknown>)) {
            (value as Record<string, unknown>)[key] = resolveStreamRefs(
                (value as Record<string, unknown>)[key],
                peer
            );
        }
        return value;
    }

    return value;
}
