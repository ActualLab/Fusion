// .NET counterpart:
//   RpcSystemCalls (239 lines) — implements IRpcSystemCalls interface; handles
//     all inbound system messages as regular RPC method calls dispatched through
//     the same inbound-call pipeline as user calls.
//
// Omitted from .NET:
//   - Reconnect() handler — receives compressed call-ID sets grouped by stage,
//     looks up each inbound call, calls TryReprocess(), and returns the IDs of
//     unknown calls.  TS doesn't implement the Reliable reconnection protocol.
//   - Cancel() handler — .NET finds the inbound call and cancels its
//     CancellationTokenSource, aborting the running handler.  TS removes the
//     inbound call from the tracker but does not yet propagate cancellation
//     to the service handler (no AbortSignal threading through dispatch).
//   - M() (Match) handler — tells an outbound call to use its cached result
//     instead of the response payload.  TS has no response caching.
//   - NotFound() — throws an EndpointNotFound error.  TS sends this as a regular
//     $sys.Error; no separate system call type.
//   - KeepAlive() / Disconnect() — manage RpcSharedObject lifetimes.  TS has no
//     shared-object tracker.
//   - IRpcPolymorphicArgumentHandler.IsValidCall — resolves the concrete
//     deserialization type for polymorphic Ok/Item/Batch arguments by looking up
//     the related outbound call's return type.  TS deserializes all args as
//     unknown via JSON.parse (inherently polymorphic).
//   - DI / IServiceProvider / RpcServiceBase — .NET system calls are resolved via
//     DI.  TS uses a class with a handle() method.

import { getLogs } from './logging.js';
import { RpcSystemCalls, type RpcMessage } from './rpc-message.js';
import type { RpcPeer } from './rpc-peer.js';
import type { RpcStream } from './rpc-stream.js';
import { resolveStreamRefs } from './rpc-stream.js';
import type { RpcStreamSender } from './rpc-stream-sender.js';
import { RpcCallStage } from './rpc-call-stage.js';
import { IncreasingSeqCompressor } from './increasing-seq-compressor.js';
import { base64Decode, base64Encode } from './base64.js';

const { debugLog, warnLog } = getLogs('RpcSystemCallHandler');

/** Handles incoming system call messages — class-based equivalent of the former standalone function. */
export class RpcSystemCallHandler {
    handle(message: RpcMessage, args: unknown[], peer: RpcPeer): void {
        const method = message.Method;
        const relatedId = message.RelatedId ?? 0;
        debugLog?.log(`'${peer.ref}': handle ${method}#${relatedId}`);

        switch (method) {
        case RpcSystemCalls.ok: {
            const call = peer.outboundCalls.get(relatedId);
            if (call !== undefined) {
                call.completedStage |= RpcCallStage.ResultReady;
                if (call.removeOnOk) {
                    peer.outboundCalls.remove(relatedId);
                }
                call.result.resolve(args[0]);
            }
            break;
        }
        case RpcSystemCalls.reconnect: {
            // Server-side `$sys.Reconnect` handler. Computes the set of call
            // IDs the inbound tracker doesn't know about, replies with a
            // byte[] result via $sys.Ok. Mirrors .NET `RpcSystemCalls.Reconnect`
            // at src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:51-85.
            //
            // Limitations vs .NET: does not validate `handshakeIndex` against
            // this peer's own handshake, and does not perform per-stage
            // re-processing of compute calls (TS has no compute-invalidation
            // tracking on the inbound side). A call ID is reported as
            // "known" iff it is still in the inbound tracker.
            this._handleReconnect(relatedId, args, peer);
            break;
        }
        case RpcSystemCalls.error: {
            const call = peer.outboundCalls.remove(relatedId);
            if (call !== undefined) {
                const errorInfo = args[0] as
                        | Record<string, unknown>
                        | undefined;
                const msg = (errorInfo?.Message ??
                        errorInfo?.message ??
                        'RPC error') as string;
                const errorType = errorInfo?.TypeRef ?? errorInfo?.typeRef;
                // Mirrors RpcSystemCalls.cs:102 — surface RpcRerouteException.
                if (errorType !== null && typeof errorType === 'object') {
                    const t = errorType as Record<string, unknown>;
                    if (t.TypeName === 'RpcRerouteException'
                        || t.typeName === 'RpcRerouteException')
                        warnLog?.log('Got RpcRerouteException from remote peer:', msg);
                }
                call.result.reject(new Error(msg));
            }
            break;
        }
        case RpcSystemCalls.cancel: {
            // Remote peer is cancelling a call it asked us to process — remove
            // from the inbound tracker.  Full cancellation propagation (aborting
            // the running service handler) is not yet implemented; this just
            // unregisters the call so we don't send a response for it.
            peer.inboundCalls.remove(relatedId);
            break;
        }
        case RpcSystemCalls.keepAlive: {
            // Remote keep-alive — mark the peer as "alive" so the peer's
            // watchdog doesn't force-close the connection. Mirrors .NET
            // `RpcObjectTrackers.KeepAlive` which sets `LastKeepAliveAt`.
            peer.notifyKeepAliveReceived();
            break;
        }
        case RpcSystemCalls.item: {
            const stream = peer.remoteObjects.get(relatedId) as
                    | RpcStream<unknown>
                    | undefined;
            // Silent no-op when no stream — mirrors RpcSystemCalls.cs:164-170.
            // This is normal during reconnect: the server may send items for
            // streams the client has already disposed.
            if (!stream)
                debugLog?.log(`$sys.I: no stream for relatedId=${relatedId}`);
            else
                stream.onItem(
                        args[0] as number,
                        resolveStreamRefs(args[1], peer)
                );
            break;
        }
        case RpcSystemCalls.batch: {
            const stream = peer.remoteObjects.get(relatedId) as
                    | RpcStream<unknown>
                    | undefined;
            if (!stream)
                debugLog?.log(`$sys.B: no stream for relatedId=${relatedId}`);
            else if (Array.isArray(args[1])) {
                const items = args[1] as unknown[];
                for (let i = 0; i < items.length; i++)
                    items[i] = resolveStreamRefs(items[i], peer);
                stream.onBatch(args[0] as number, items);
            }
            break;
        }
        case RpcSystemCalls.end: {
            const stream = peer.remoteObjects.get(relatedId) as
                    | RpcStream<unknown>
                    | undefined;
            if (!stream) {
                debugLog?.log(`$sys.End: no stream for relatedId=${relatedId}`);
            } else {
                // .NET ExceptionInfo is a struct — even for normal completion, it serializes
                // as a non-null object with empty fields (e.g. { "message": "", "typeRef": {...} }).
                // Check both PascalCase and camelCase, and treat empty messages as no error.
                const errorInfo = args[1] as Record<string, unknown> | null;
                const msg = (errorInfo?.Message ?? errorInfo?.message) as
                        | string
                        | undefined;
                const error = msg ? new Error(msg) : null;
                stream.onEnd(args[0] as number, error);
            }
            break;
        }
        case RpcSystemCalls.ack: {
            const sender = peer.sharedObjects.get(relatedId) as
                    | RpcStreamSender<unknown>
                    | undefined;
            sender?.onAck(args[0] as number, args[1] as string);
            break;
        }
        case RpcSystemCalls.ackEnd: {
            const sender = peer.sharedObjects.get(relatedId) as
                    | RpcStreamSender<unknown>
                    | undefined;
            sender?.onAckEnd(args[0] as string);
            break;
        }
        default: {
            if (
                method === '$sys.Disconnect' ||
                    method?.startsWith('$sys.Disconnect')
            ) {
                // Server is telling us the listed remote object IDs have been torn down
                // on its side (e.g. shared stream closed).  Propagate disconnect() to the
                // matching remote objects so any pending consumers (for-await loops) exit
                // cleanly instead of hanging.
                const ids = args[0] as number[] | undefined;
                if (Array.isArray(ids)) {
                    for (const id of ids) {
                        const remoteObj = peer.remoteObjects.get(id);
                        if (
                            remoteObj &&
                                typeof remoteObj.disconnect === 'function'
                        ) {
                            remoteObj.disconnect();
                        }
                        // Also check shared objects — server may disconnect a client-to-server
                        // stream sender (e.g. audio stream) after pod restart or timeout.
                        const sharedObj = peer.sharedObjects.get(id);
                        if (
                            sharedObj &&
                                typeof sharedObj.disconnect === 'function'
                        ) {
                            sharedObj.disconnect();
                        }
                    }
                }
            }
            break;
        }
        }
    }

    /**
     * Server-side handler for the `$sys.Reconnect` call. Parses the peer's
     * report of in-flight call IDs (grouped by stage, IncreasingSeqCompressor-
     * encoded, base64-wrapped for JSON transport), computes the subset this
     * peer does not recognize, and replies with a compressed byte[] of the
     * unknown IDs wrapped in `$sys.Ok`.
     *
     * @param relatedId The outbound call ID the client used for this
     *                  `$sys.Reconnect` invocation — we send `$sys.Ok` back
     *                  with the same `RelatedId`.
     */
    private _handleReconnect(relatedId: number, args: unknown[], peer: RpcPeer): void {
        if (peer.connection === undefined) return;

        // args[0]: handshakeIndex (number) — not validated in TS, see class comment.
        // args[1]: completedStagesData — shape depends on wire format.
        //    JSON:    { [stage: string]: base64-string }
        //    msgpack: Map-like with int keys and Uint8Array values (unsupported in TS).
        let unknownIds: number[] = [];
        try {
            const stagesRaw = args[1];
            if (stagesRaw && typeof stagesRaw === 'object' && !Array.isArray(stagesRaw)) {
                const unknownSet = new Set<number>();
                for (const [, rawValue] of Object.entries(stagesRaw as Record<string, unknown>)) {
                    const bytes = _toBytes(rawValue);
                    if (bytes === null) continue;
                    const callIds = IncreasingSeqCompressor.deserialize(bytes);
                    for (const callId of callIds) {
                        if (peer.inboundCalls.get(callId) === undefined) {
                            unknownSet.add(callId);
                        }
                    }
                }
                unknownIds = [...unknownSet].sort((a, b) => a - b);
            }
        } catch {
            // If parsing fails, report all as unknown (safest behavior — client
            // will resend everything).
            // Client handles this via its own try/catch around the reconcile call.
        }

        const responseBytes = IncreasingSeqCompressor.serialize(unknownIds);
        const responseValue = peer.serializationFormat.isBinary ? responseBytes : base64Encode(responseBytes);
        peer.hub.systemCallSender.ok(peer.connection, peer.serializationFormat, relatedId, responseValue);
    }
}

/**
 * Coerce a wire value carrying a byte[] into a Uint8Array.
 *  - JSON format sends byte[] as a base64 string.
 *  - msgpack format sends byte[] as native `bin` (Uint8Array).
 * Returns `null` if the value is neither.
 */
function _toBytes(value: unknown): Uint8Array | null {
    if (value instanceof Uint8Array) return value;
    if (typeof value === 'string') {
        try { return base64Decode(value); } catch { return null; }
    }
    return null;
}
