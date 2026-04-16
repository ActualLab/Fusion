// .NET counterparts:
//   RpcConnection — thin wrapper around RpcTransport + PropertyBag.  RpcTransport
//     is an abstract IAsyncEnumerable<RpcInboundMessage> + Send() that can be
//     WebSocket-based or in-memory.  The transport also handles frame batching and
//     back-pressure via a Channel<T> send queue.
//
// Omitted from .NET:
//   - PropertyBag on connection — used in .NET for per-connection metadata (e.g.
//     authentication info attached by middleware).  TS has no middleware pipeline,
//     so no need for an extensible property bag.
//   - IsLocal flag — .NET distinguishes local loopback connections (same-process
//     server) from remote ones.  TS is always a remote client.
//   - RpcTransport as IAsyncEnumerable — .NET reads inbound messages via
//     async iteration with cancellation.  TS uses event-based delivery
//     (messageReceived handler) because browser WebSocket API is event-driven.
//   - Channel<RpcOutboundMessage> send queue with backpressure — .NET buffers
//     outbound messages in an async channel that the transport drains.  TS sends
//     synchronously on the WebSocket; if the socket isn't ready, messages are
//     buffered in _sendBuffer (CONNECTING) or silently dropped (CLOSING/CLOSED).
//     JS is single-threaded so there's no contention, and WebSocket.send() itself
//     handles buffering at the OS level.
//   - IAsyncDisposable — .NET transports are disposable resources.  TS connections
//     are closed via close() and garbage-collected.

import { Encoder, Decoder } from '@msgpack/msgpack';
import { PromiseSource, EventHandlerSet } from '@actuallab/core';
import { getLogs } from './logging.js';
import {
    splitFrame,
    serializeFrame,
    splitBinaryFrame as splitBinaryFrameFn,
    serializeBinaryFrame,
    createBinaryEncoder,
} from './rpc-serialization.js';
import type { RpcMessage } from './rpc-message.js';
import type { RpcSerializationFormat } from './rpc-serialization-format.js';
import type { RpcMethodRegistry } from './rpc-method-registry.js';

const { warnLog } = getLogs('RpcConnection');

/** Abstract WebSocket interface — works with both browser WebSocket and Node.js ws. */
export interface WebSocketLike {
    readonly readyState: number;
    binaryType?: string;
    send(data: string | ArrayBufferLike | Uint8Array | ArrayBufferView): void;
    close(code?: number, reason?: string): void;
    onopen: ((ev: unknown) => void) | null;
    onmessage: ((ev: { data: unknown }) => void) | null;
    onclose: ((ev: { code: number; reason: string }) => void) | null;
    onerror: ((ev: unknown) => void) | null;
}

export const WebSocketState = {
    CONNECTING: 0,
    OPEN: 1,
    CLOSING: 2,
    CLOSED: 3,
} as const;

/** Received message — either text (string) or binary (already parsed). */
export type RpcReceivedMessage =
    | { kind: 'text'; raw: string }
    | { kind: 'binary'; message: RpcMessage; args: unknown[] };

/** Abstract RPC connection — transport-agnostic interface for sending/receiving messages. */
export interface RpcConnection {
    readonly isOpen: boolean;
    readonly binaryMode: boolean;
    readonly whenConnected: Promise<void>;
    readonly messageReceived: EventHandlerSet<RpcReceivedMessage>;
    readonly closed: EventHandlerSet<{ code: number; reason: string }>;
    /** Optional reusable msgpack encoder for outbound binary messages.
     *  Callers passing this to `serializeBinaryMessage` skip the per-call
     *  Encoder construction + resizeBuffer growth. Implementations that
     *  don't support binary serialization can leave it undefined. */
    readonly encoder?: Encoder;
    send(serializedMessage: string): void;
    sendBinary(data: Uint8Array): void;
    close(code?: number, reason?: string): void;
}

/** WebSocket-based RpcConnection — handles frame splitting, binary/text modes, and message queueing. */
export class RpcWebSocketConnection implements RpcConnection {
    private _ws: WebSocketLike;
    private _sendBuffer: (string | Uint8Array)[] = [];
    private _connected = new PromiseSource<void>();

    /** Per-connection msgpack encoder — reused across outbound messages to
     *  amortise construction cost and to keep the internal write buffer at
     *  its largest observed size (avoids repeated `resizeBuffer` growth on
     *  large video frames). See serializeBinaryMessage() for details. */
    private _encoder: Encoder = createBinaryEncoder();

    /** Per-connection msgpack decoder — reused across inbound messages
     *  rather than reconstructing one for every `decodeMulti` call. */
    private _decoder: Decoder = new Decoder();

    /** Exposed for callers who need to encode outbound binary messages
     *  through this connection (e.g. `serializeBinaryMessage`). Safe to
     *  pass to synchronous encode calls on the same event-loop turn. */
    get encoder(): Encoder {
        return this._encoder;
    }
    get decoder(): Decoder {
        return this._decoder;
    }

    private _format: RpcSerializationFormat | undefined;
    private _methodRegistry: RpcMethodRegistry | undefined;

    readonly binaryMode: boolean;
    readonly messageReceived = new EventHandlerSet<RpcReceivedMessage>();
    readonly closed = new EventHandlerSet<{ code: number; reason: string }>();
    readonly error = new EventHandlerSet<unknown>();

    constructor(
        ws: WebSocketLike,
        binaryMode = false,
        format?: RpcSerializationFormat,
        methodRegistry?: RpcMethodRegistry
    ) {
        this._ws = ws;
        this.binaryMode = binaryMode;
        this._format = format;
        this._methodRegistry = methodRegistry;

        if (binaryMode && ws.binaryType !== undefined)
            ws.binaryType = 'arraybuffer';

        if (ws.readyState === WebSocketState.OPEN) {
            this._connected.resolve();
            this._flush();
        }

        ws.onopen = () => {
            this._connected.resolve();
            this._flush();
        };

        ws.onmessage = ev => {
            // Binary frame — V5 self-delimiting envelopes (one or more per WS frame).
            //
            // Accept BOTH `ArrayBuffer` (browser WebSocket when `binaryType` is
            // 'arraybuffer') AND `Uint8Array`-shaped views (Node `ws` delivering a
            // `Buffer`, which is a `Uint8Array` subclass). The node-ws adapter
            // intentionally passes Buffer through without an ArrayBuffer copy —
            // at 300 concurrent video streams that copy was ~100 MB/s of pure
            // memcpy + allocation pressure. Matching `ArrayBuffer.isView` here
            // keeps the zero-copy path alive all the way to `splitBinaryFrame`.
            if (
                ev.data instanceof Uint8Array ||
                ev.data instanceof ArrayBuffer
            ) {
                // Node `Buffer` is a `Uint8Array` subclass, so we can hand it to
                // `splitBinaryFrame` directly with no view wrapping or copying.
                // Browsers deliver `ArrayBuffer`, which needs a single zero-copy
                // view wrap.
                const frame =
                    ev.data instanceof Uint8Array
                        ? ev.data
                        : new Uint8Array(ev.data);
                try {
                    const messages = this._splitBinary(frame);
                    for (const { message, args } of messages) {
                        this.messageReceived.trigger({
                            kind: 'binary',
                            message,
                            args,
                        });
                    }
                } catch (e) {
                    warnLog?.log('Failed to split binary frame:', e);
                    this.error.trigger(e);
                }
            } else if (
                typeof Blob !== 'undefined' &&
                ev.data instanceof Blob &&
                this.binaryMode
            ) {
                // Fallback: some Chromium builds deliver the first frames as Blob even
                // after setting binaryType='arraybuffer'. Convert and route through
                // the binary path so we don't silently drop them.
                void ev.data.arrayBuffer().then(ab => {
                    const frame = new Uint8Array(ab);
                    try {
                        const messages = this._splitBinary(frame);
                        for (const { message, args } of messages) {
                            this.messageReceived.trigger({
                                kind: 'binary',
                                message,
                                args,
                            });
                        }
                    } catch (e) {
                        warnLog?.log('Failed to split binary blob frame:', e);
                        this.error.trigger(e);
                    }
                });
            } else {
                // Text frame — JSON delimited messages
                const data =
                    typeof ev.data === 'string' ? ev.data : String(ev.data);
                const messages = splitFrame(data);
                for (const msg of messages) {
                    if (msg.length > 0)
                        this.messageReceived.trigger({
                            kind: 'text',
                            raw: msg,
                        });
                }
            }
        };

        ws.onclose = ev => {
            this.closed.trigger({ code: ev.code, reason: ev.reason });
        };

        ws.onerror = ev => {
            this.error.trigger(ev);
        };
    }

    get isOpen(): boolean {
        return this._ws.readyState === WebSocketState.OPEN;
    }

    get whenConnected(): Promise<void> {
        return this._connected.promise;
    }

    send(serializedMessage: string): void {
        this._sendRaw(serializedMessage);
    }

    sendBinary(data: Uint8Array): void {
        this._sendRaw(data);
    }

    sendTextBatch(messages: string[]): void {
        this._sendRaw(serializeFrame(messages));
    }

    sendBinaryBatch(messages: Uint8Array[]): void {
        this._sendRaw(serializeBinaryFrame(messages));
    }

    /** Split a binary frame using the format (compact-aware) or default splitter. */
    private _splitBinary(
        frame: Uint8Array
    ): { message: RpcMessage; args: unknown[] }[] {
        if (this._format) {
            return this._format.splitBinaryFrame(
                frame,
                this._decoder,
                this._methodRegistry
            );
        }
        return splitBinaryFrameFn(frame, this._decoder);
    }

    close(code?: number, reason?: string): void {
        this._ws.close(code, reason);
    }

    private _sendRaw(data: string | Uint8Array): void {
        try {
            if (this._ws.readyState === WebSocketState.OPEN) {
                this._ws.send(data);
            } else if (this._ws.readyState === WebSocketState.CONNECTING)
                this._sendBuffer.push(data);
            // CLOSING/CLOSED: silently drop
        } catch {
            // Swallow — disconnect event handles cleanup
        }
    }

    private _flush(): void {
        if (this._sendBuffer.length === 0) return;
        const buffer = this._sendBuffer;
        this._sendBuffer = [];
        try {
            for (const item of buffer) this._ws.send(item);
        } catch {
            // Swallow — disconnect event handles cleanup
        }
    }
}
