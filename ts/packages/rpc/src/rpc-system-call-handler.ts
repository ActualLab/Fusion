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

import { RpcSystemCalls, type RpcMessage } from "./rpc-message.js";
import type { RpcPeer } from "./rpc-peer.js";
import type { RpcStream } from "./rpc-stream.js";
import { resolveStreamRefs } from "./rpc-stream.js";
import type { RpcStreamSender } from "./rpc-stream-sender.js";

/** Handles incoming system call messages — class-based equivalent of the former standalone function. */
export class RpcSystemCallHandler {
  handle(message: RpcMessage, args: unknown[], peer: RpcPeer): void {
    const method = message.Method;
    const relatedId = message.RelatedId ?? 0;

    switch (method) {
      case RpcSystemCalls.ok: {
        const call = peer.outbound.get(relatedId);
        if (call !== undefined) {
          if (call.removeOnOk) {
            peer.outbound.remove(relatedId);
          }
          call.result.resolve(args[0]);
        }
        break;
      }
      case RpcSystemCalls.error: {
        const call = peer.outbound.remove(relatedId);
        if (call !== undefined) {
          const errorInfo = args[0] as Record<string, unknown> | undefined;
          const msg = (errorInfo?.Message ?? errorInfo?.message ?? "RPC error") as string;
          call.result.reject(new Error(msg));
        }
        break;
      }
      case RpcSystemCalls.cancel: {
        // Remote peer is cancelling a call it asked us to process — remove
        // from the inbound tracker.  Full cancellation propagation (aborting
        // the running service handler) is not yet implemented; this just
        // unregisters the call so we don't send a response for it.
        peer.inbound.remove(relatedId);
        break;
      }
      case RpcSystemCalls.keepAlive: {
        // Remote keep-alive — nothing to do, just acknowledges the connection is alive
        break;
      }
      case RpcSystemCalls.item: {
        const stream = peer.remoteObjects.get(relatedId) as RpcStream<unknown> | undefined;
        if (stream) stream.onItem(args[0] as number, resolveStreamRefs(args[1], peer));
        break;
      }
      case RpcSystemCalls.batch: {
        const stream = peer.remoteObjects.get(relatedId) as RpcStream<unknown> | undefined;
        if (stream) {
          const items = args[1] as unknown[];
          for (let i = 0; i < items.length; i++) items[i] = resolveStreamRefs(items[i]!, peer);
          stream.onBatch(args[0] as number, items);
        }
        break;
      }
      case RpcSystemCalls.end: {
        const stream = peer.remoteObjects.get(relatedId) as RpcStream<unknown> | undefined;
        if (stream) {
          // .NET ExceptionInfo is a struct — even for normal completion, it serializes
          // as a non-null object with empty fields (e.g. { "message": "", "typeRef": {...} }).
          // Check both PascalCase and camelCase, and treat empty messages as no error.
          const errorInfo = args[1] as Record<string, unknown> | null;
          const msg = (errorInfo?.Message ?? errorInfo?.message) as string | undefined;
          const error = msg ? new Error(msg) : null;
          stream.onEnd(args[0] as number, error);
        }
        break;
      }
      case RpcSystemCalls.ack: {
        const sender = peer.sharedObjects.get(relatedId) as RpcStreamSender<unknown> | undefined;
        sender?.onAck(args[0] as number, args[1] as string);
        break;
      }
      case RpcSystemCalls.ackEnd: {
        const sender = peer.sharedObjects.get(relatedId) as RpcStreamSender<unknown> | undefined;
        sender?.onAckEnd(args[0] as string);
        break;
      }
    }
  }
}
