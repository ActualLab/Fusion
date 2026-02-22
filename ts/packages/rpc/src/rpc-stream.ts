import { PromiseSource } from "@actuallab/core";
import type { RpcObjectId, IRpcObject } from "./rpc-object.js";
import { RpcObjectKind } from "./rpc-object.js";
import type { RpcPeer } from "./rpc-peer.js";

/** Parsed stream reference from the server's result payload. */
export interface RpcStreamRef {
  readonly hostId: string;
  readonly localId: number;
  readonly ackPeriod: number;
  readonly ackAdvance: number;
}

/**
 * Parses a stream reference string of the form "{hostId},{localId},{ackPeriod},{ackAdvance}".
 * Returns null if the value is not a valid stream reference.
 */
export function parseStreamRef(value: unknown): RpcStreamRef | null {
  if (typeof value !== "string") return null;
  const parts = value.split(",");
  if (parts.length !== 4) return null;
  const hostId = parts[0]!;
  const localId = parseInt(parts[1]!, 10);
  const ackPeriod = parseInt(parts[2]!, 10);
  const ackAdvance = parseInt(parts[3]!, 10);
  if (isNaN(localId) || isNaN(ackPeriod) || isNaN(ackAdvance)) return null;
  return { hostId, localId, ackPeriod, ackAdvance };
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
  readonly peer: RpcPeer;
  readonly ackPeriod: number;
  readonly ackAdvance: number;

  private _buffer: T[] = [];
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
    this.peer = peer;
    this.ackPeriod = ref.ackPeriod;
    this.ackAdvance = ref.ackAdvance;
  }

  /** Called by system call handler when a single item arrives ($sys.I). */
  onItem(index: number, item: T): void {
    if (this._disposed || this._completed) return;

    if (index > this._nextExpectedIndex) {
      // Gap — request reset from the server
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
    // Re-request from where we left off
    this._sendAck(this._nextExpectedIndex, true);
  }

  disconnect(): void {
    if (this._completed) return;
    this._completed = true;
    this._completionError = new Error("Peer disconnected.");
    this._notifyConsumer();
  }

  // -- AsyncIterable --

  [Symbol.asyncIterator](): AsyncIterator<T> {
    if (this._iterating) throw new Error("RpcStream can only be iterated once.");
    this._iterating = true;

    let bufferIndex = 0;
    let consumedIndex = 0;

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
          if (bufferIndex < self._buffer.length) {
            const value = self._buffer[bufferIndex]!;
            bufferIndex++;
            consumedIndex++;
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

  private _sendAck(nextIndex: number, _mustReset: boolean): void {
    this._ackSentUpTo = nextIndex;
    const conn = this.peer.connection;
    if (conn) {
      this.peer.hub.systemCallSender.ack(conn, this.id.localId, nextIndex, this.id.hostId);
    }
  }

  private _sendAckEnd(): void {
    const conn = this.peer.connection;
    if (conn) {
      this.peer.hub.systemCallSender.ackEnd(conn, this.id.localId, this.id.hostId);
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

  if (typeof value === "string") {
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

  if (typeof value === "object") {
    for (const key of Object.keys(value as Record<string, unknown>)) {
      (value as Record<string, unknown>)[key] = resolveStreamRefs(
        (value as Record<string, unknown>)[key], peer,
      );
    }
    return value;
  }

  return value;
}
