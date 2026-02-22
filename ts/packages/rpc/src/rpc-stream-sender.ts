import { PromiseSource } from "@actuallab/core";
import type { RpcObjectId, IRpcObject } from "./rpc-object.js";
import { RpcObjectKind } from "./rpc-object.js";
import type { RpcPeer } from "./rpc-peer.js";

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
  readonly id: RpcObjectId;
  readonly kind = RpcObjectKind.Local;
  readonly peer: RpcPeer;
  readonly ackPeriod: number;
  readonly ackAdvance: number;

  private _nextIndex = 0;
  private _ended = false;
  private _started = new PromiseSource<void>();

  constructor(peer: RpcPeer, ackPeriod = DEFAULT_ACK_PERIOD, ackAdvance = DEFAULT_ACK_ADVANCE) {
    const localId = peer.sharedObjects.nextId();
    this.id = { hostId: peer.hub.hubId, localId };
    this.peer = peer;
    this.ackPeriod = ackPeriod;
    this.ackAdvance = ackAdvance;
  }

  /** Returns the stream reference string for the $sys.Ok response. */
  toRef(): string {
    return `${this.id.hostId},${this.id.localId},${this.ackPeriod},${this.ackAdvance}`;
  }

  /** Called by system call handler when $sys.Ack is received from the client. */
  onAck(_nextIndex: number, _hostId: string): void {
    if (!this._started.isCompleted) {
      this._started.resolve();
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
    this.peer.hub.systemCallSender.item(conn, this.id.localId, this._nextIndex, item);
    this._nextIndex++;
  }

  /** Send a batch of items to the client. */
  sendBatch(items: T[]): void {
    if (this._ended || items.length === 0) return;
    const conn = this.peer.connection;
    if (!conn) return;
    this.peer.hub.systemCallSender.batch(conn, this.id.localId, this._nextIndex, items);
    this._nextIndex += items.length;
  }

  /** Signal stream completion to the client. */
  sendEnd(error?: Error | null): void {
    if (this._ended) return;
    this._ended = true;
    const conn = this.peer.connection;
    if (!conn) return;
    const errorInfo = error ? { Message: error.message } : null;
    this.peer.hub.systemCallSender.end(conn, this.id.localId, this._nextIndex, errorInfo);
  }

  /**
   * Consume an AsyncIterable and send all items to the client.
   * Waits for the client's initial Ack before starting to pump items.
   */
  async writeFrom(source: AsyncIterable<T>): Promise<void> {
    await this._started.promise;

    try {
      for await (const item of source) {
        if (this._ended) return;
        this.sendItem(item);
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
