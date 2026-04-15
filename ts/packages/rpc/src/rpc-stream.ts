import Denque from 'denque';
import { PromiseSource } from '@actuallab/core';
import type { RpcObjectId, IRpcObject } from './rpc-object.js';
import { RpcObjectKind } from './rpc-object.js';
import type { RpcPeer } from './rpc-peer.js';

/** Default ack period (matches .NET RpcStream defaults). */
const DEFAULT_ACK_PERIOD = 30;
/** Default ack advance (matches .NET RpcStream defaults). */
const DEFAULT_ACK_ADVANCE = 61;

/** Parsed stream reference from the server's result payload. */
export interface RpcStreamRef {
    readonly hostId: string;
    readonly localId: number;
    readonly ackPeriod: number;
    readonly ackAdvance: number;
    readonly allowReconnect: boolean;
    readonly isRealTime: boolean;
}

/** Configuration options for creating a local (origin-side) RpcStream. */
export interface RpcStreamOptions<T> {
    ackPeriod?: number;
    ackAdvance?: number;
    allowReconnect?: boolean;
    isRealTime?: boolean;
    canSkipTo?: (item: T) => boolean;
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
 * Dual-mode RPC stream — works as both local (origin-side) and remote (target-side).
 *
 * **Local mode**: wraps an AsyncIterable and carries streaming configuration
 * (IsRealTime, CanSkipTo, etc.). Created by service methods and sent to the
 * remote peer via RpcStreamSender.
 *
 * **Remote mode**: consumes items sent by the server via system calls
 * ($sys.I, $sys.B, $sys.End) and implements AsyncIterable for for-await-of consumption.
 *
 * This mirrors the .NET RpcStream<T> design where the same type serves both roles.
 */
export class RpcStream<T> implements AsyncIterable<T>, IRpcObject {
    readonly id: RpcObjectId;
    readonly kind: RpcObjectKind;
    readonly allowReconnect: boolean;
    readonly isRealTime: boolean;
    readonly canSkipTo: (item: T) => boolean;
    readonly ackPeriod: number;
    readonly ackAdvance: number;

    // Local (origin) mode
    readonly localSource?: AsyncIterable<T>;

    // Remote (target) mode
    readonly peer!: RpcPeer;

    // Remote-only state
    private _buffer: Denque<T> = new Denque<T>();
    private _nextExpectedIndex = 0;
    private _consumerWaiting: PromiseSource<void> | null = null;
    private _completed = false;
    private _completionError: Error | null = null;
    private _disposed = false;
    private _started = false;
    private _iterating = false;
    private _ackSentUpTo = 0;

    /** Create a local (origin-side) stream wrapping an async iterable with optional configuration. */
    constructor(source: AsyncIterable<T>, options?: RpcStreamOptions<T>);
    /** Create a remote (target-side) stream from a parsed stream reference and peer. */
    constructor(ref: RpcStreamRef, peer: RpcPeer);
    constructor(
        sourceOrRef: AsyncIterable<T> | RpcStreamRef,
        optionsOrPeer?: RpcStreamOptions<T> | RpcPeer,
    ) {
        if (_isAsyncIterable(sourceOrRef)) {
            // Local (origin) mode
            const options = (optionsOrPeer as RpcStreamOptions<T> | undefined) ?? {};
            this.kind = RpcObjectKind.Local;
            this.localSource = sourceOrRef;
            this.id = { hostId: '', localId: 0 };
            this.allowReconnect = options.allowReconnect ?? true;
            this.isRealTime = options.isRealTime ?? false;
            this.canSkipTo = options.canSkipTo ?? (() => true);
            this.ackPeriod = options.ackPeriod ?? DEFAULT_ACK_PERIOD;
            this.ackAdvance = options.ackAdvance ?? DEFAULT_ACK_ADVANCE;
        } else {
            // Remote (target) mode
            const ref = sourceOrRef;
            const peer = optionsOrPeer as RpcPeer;
            this.kind = RpcObjectKind.Remote;
            this.id = { hostId: ref.hostId, localId: ref.localId };
            this.peer = peer;
            this.allowReconnect = ref.allowReconnect;
            this.isRealTime = ref.isRealTime;
            this.canSkipTo = () => true; // not serialized
            this.ackPeriod = ref.ackPeriod;
            this.ackAdvance = ref.ackAdvance;
        }
    }

    /** Called by system call handler when a single item arrives ($sys.I). */
    onItem(index: number, item: T): void {
        if (this._disposed || this._completed) return;

        if (index > this._nextExpectedIndex) {
            console.warn(
                `[RpcStream] item index gap: localId=${this.id.localId}, expected=${this._nextExpectedIndex}, received=${index}`
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
        if (this.kind === RpcObjectKind.Local) return; // no-op for local streams
        if (this.allowReconnect) {
            this._sendAck(this._nextExpectedIndex, true);
        } else {
            this.disconnect();
        }
    }

    disconnect(): void {
        if (this.kind === RpcObjectKind.Local) return; // no-op for local streams
        if (this._completed) return;
        this._completed = true;
        this._completionError = new Error('Peer disconnected.');
        this._notifyConsumer();
    }

    // -- AsyncIterable --

    [Symbol.asyncIterator](): AsyncIterator<T> {
        // Local mode: delegate to the local source
        if (this.kind === RpcObjectKind.Local) {
            return this.localSource![Symbol.asyncIterator]();
        }

        // Remote mode: buffer-based consumption
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
        if (this.kind === RpcObjectKind.Remote) {
            this.peer.remoteObjects.unregister(this);
        }
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

/** @internal Type guard for AsyncIterable (has Symbol.asyncIterator). */
function _isAsyncIterable(value: unknown): value is AsyncIterable<unknown> {
    return (
        value !== null &&
        typeof value === 'object' &&
        Symbol.asyncIterator in (value as object)
    );
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
