// .NET counterpart:
//   RpcSystemCallSender (202 lines) — a DI-registered service that sends system
//     calls by creating RpcOutboundContext + calling PrepareCallForSendNoWait +
//     serialising via the full outbound message pipeline.
//
// Omitted from .NET:
//   - Complete<TResult>() / typed Ok<TResult>() — .NET serialises the Ok result
//     using the method's unwrapped return type for proper polymorphic handling.
//     TS uses JSON.stringify(result) which handles any type uniformly.
//   - Match() — sends $sys.M when the response hash matches a cached entry,
//     avoiding re-sending the full payload.  TS has no response caching.
//   - NotFound() — TS sends "Service not found" / "Method not found" as $sys.Error.
//   - Disconnect() — sends $sys.Disconnect with object IDs for shared-object
//     lifetime management.  TS has no shared-object tracker.
//   - Ack() / AckEnd() / Item<T>() / Batch<T>() / End() — stream control
//     messages for RpcStream.  TS has no streaming support yet.
//   - RpcOutboundContext / PrepareCallForSendNoWait pipeline — .NET creates a full
//     outbound context with MethodDef, ArgumentList, etc., then serialises via the
//     peer's MessageSerializer.  TS directly calls serializeMessage() (JSON) since
//     there's only one serialization format.
//   - Tracing / CallLogger integration — .NET logs each system call through the
//     peer's CallLogger.  TS has no tracing infrastructure.
//   - StopMode-aware Error() — .NET checks peer.StopMode to decide whether to
//     suppress error responses when the peer is shutting down.  TS has no stop
//     mode concept.

import { RpcSystemCalls, type RpcMessage } from './rpc-message.js';
import type { RpcConnection } from './rpc-connection.js';
import type { RpcSerializationFormat } from './rpc-serialization-format.js';
import type { RpcMethodRegistry } from './rpc-method-registry.js';

/**
 * Sends system RPC messages — like .NET's RpcSystemCallSender.
 *
 * Hot-path allocation notes: `item`, `batch`, `ack`, and `end` are called
 * once per stream frame (or once per ack window). Naively building a fresh
 * `{ Method, RelatedId }` envelope object plus a fresh two-element arg array
 * on every call showed up as ~2-4% self time + non-trivial GC pressure in
 * the 300-pull video-load-test profile.
 *
 * The sender itself is a per-Hub singleton and `_send`/format.serializeMessage
 * are fully synchronous (no awaits, no re-entrancy on a single-threaded event
 * loop), so we reuse a small pool of preallocated envelope + arg-array slots
 * per hot method. Mutate fields just before the send call; the encoder reads
 * them synchronously and produces a detached copy, after which the slot is
 * free to be mutated again on the next call.
 */
export class RpcSystemCallSender {
    // Reusable envelopes — one per hot-path method.
    private readonly _itemEnv: RpcMessage = {
        Method: RpcSystemCalls.item,
        RelatedId: 0,
    };
    private readonly _batchEnv: RpcMessage = {
        Method: RpcSystemCalls.batch,
        RelatedId: 0,
    };
    private readonly _endEnv: RpcMessage = {
        Method: RpcSystemCalls.end,
        RelatedId: 0,
    };
    private readonly _ackEnv: RpcMessage = {
        Method: RpcSystemCalls.ack,
        RelatedId: 0,
    };
    private readonly _ackEndEnv: RpcMessage = {
        Method: RpcSystemCalls.ackEnd,
        RelatedId: 0,
    };

    // Reusable arg arrays.
    private readonly _itemArgs: [number, unknown] = [0, null];
    private readonly _batchArgs: [number, unknown] = [0, null];
    private readonly _endArgs: [number, { TypeRef: string; Message: string }] =
        [0, { TypeRef: '', Message: '' }];
    private readonly _ackArgs: [number, string] = [0, ''];
    private readonly _ackEndArgs: [string] = [''];

    private static readonly NO_ERROR: { TypeRef: string; Message: string } =
        Object.freeze({ TypeRef: '', Message: '' }) as unknown as {
            TypeRef: string;
            Message: string;
        };

    registry?: RpcMethodRegistry;

    private _send(
        conn: RpcConnection,
        format: RpcSerializationFormat,
        envelope: RpcMessage,
        args?: unknown[]
    ): void {
        const wireData = format.serializeMessage(
            envelope,
            args,
            conn.encoder,
            this.registry
        );
        if (typeof wireData === 'string') {
            conn.send(wireData);
        } else {
            conn.sendBinary(wireData);
        }
    }

    handshake(
        conn: RpcConnection,
        format: RpcSerializationFormat,
        peerId: string,
        hubId: string,
        index: number
    ): void {
        // RpcHandshake uses [MessagePackObject] array mode for binary.
        const handshakeArg = format.isBinary
            ? [peerId, null, hubId, 2, index]
            : {
                RemotePeerId: peerId,
                RemoteApiVersionSet: null,
                RemoteHubId: hubId,
                ProtocolVersion: 2,
                Index: index,
            };
        this._send(conn, format, { Method: RpcSystemCalls.handshake }, [
            handshakeArg,
        ]);
    }

    ok(
        conn: RpcConnection,
        format: RpcSerializationFormat,
        relatedId: number,
        result: unknown
    ): void {
        this._send(
            conn,
            format,
            { Method: RpcSystemCalls.ok, RelatedId: relatedId },
            [result]
        );
    }

    error(
        conn: RpcConnection,
        format: RpcSerializationFormat,
        relatedId: number,
        error: unknown
    ): void {
        const message = error instanceof Error ? error.message : String(error);
        this._send(
            conn,
            format,
            { Method: RpcSystemCalls.error, RelatedId: relatedId },
            [{ Message: message }]
        );
    }

    cancel(
        conn: RpcConnection,
        format: RpcSerializationFormat,
        relatedId: number
    ): void {
        this._send(conn, format, {
            Method: RpcSystemCalls.cancel,
            RelatedId: relatedId,
        });
    }

    keepAlive(
        conn: RpcConnection,
        format: RpcSerializationFormat,
        activeCallIds: number[]
    ): void {
        this._send(conn, format, { Method: RpcSystemCalls.keepAlive }, [
            activeCallIds,
        ]);
    }

    item(
        conn: RpcConnection,
        format: RpcSerializationFormat,
        localId: number,
        index: number,
        item: unknown
    ): void {
        this._itemEnv.RelatedId = localId;
        this._itemArgs[0] = index;
        this._itemArgs[1] = item;
        this._send(conn, format, this._itemEnv, this._itemArgs);
        this._itemArgs[1] = null;
    }

    batch(
        conn: RpcConnection,
        format: RpcSerializationFormat,
        localId: number,
        index: number,
        items: unknown[]
    ): void {
        this._batchEnv.RelatedId = localId;
        this._batchArgs[0] = index;
        this._batchArgs[1] = items;
        this._send(conn, format, this._batchEnv, this._batchArgs);
        this._batchArgs[1] = null;
    }

    end(
        conn: RpcConnection,
        format: RpcSerializationFormat,
        localId: number,
        index: number,
        error: {
            TypeRef: string;
            Message: string;
        } = RpcSystemCallSender.NO_ERROR
    ): void {
        this._endEnv.RelatedId = localId;
        this._endArgs[0] = index;
        this._endArgs[1] = error;
        this._send(conn, format, this._endEnv, this._endArgs);
    }

    ack(
        conn: RpcConnection,
        format: RpcSerializationFormat,
        localId: number,
        nextIndex: number,
        hostId: string
    ): void {
        this._ackEnv.RelatedId = localId;
        this._ackArgs[0] = nextIndex;
        this._ackArgs[1] = hostId;
        this._send(conn, format, this._ackEnv, this._ackArgs);
    }

    ackEnd(
        conn: RpcConnection,
        format: RpcSerializationFormat,
        localId: number,
        hostId: string
    ): void {
        this._ackEndEnv.RelatedId = localId;
        this._ackEndArgs[0] = hostId;
        this._send(conn, format, this._ackEndEnv, this._ackEndArgs);
    }
}
