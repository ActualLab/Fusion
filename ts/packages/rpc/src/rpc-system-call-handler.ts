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
//   - Ack() / AckEnd() / I() / B() / End() — stream control for RpcStream
//     (server→client IAsyncEnumerable).  Not ported; TS has no streaming yet.
//   - IRpcPolymorphicArgumentHandler.IsValidCall — resolves the concrete
//     deserialization type for polymorphic Ok/Item/Batch arguments by looking up
//     the related outbound call's return type.  TS deserializes all args as
//     unknown via JSON.parse (inherently polymorphic).
//   - DI / IServiceProvider / RpcServiceBase — .NET system calls are resolved via
//     DI.  TS uses a plain function.

import { RpcSystemCalls, type RpcMessage } from "./rpc-message.js";
import type { RpcOutboundCallTracker, RpcInboundCallTracker } from "./rpc-call-tracker.js";

/** Handles an incoming system call message and dispatches to the appropriate tracker. */
export function handleSystemCall(
  message: RpcMessage,
  args: unknown[],
  outboundTracker: RpcOutboundCallTracker,
  inboundTracker: RpcInboundCallTracker,
): void {
  const method = message.Method;
  const relatedId = message.RelatedId ?? 0;

  switch (method) {
    case RpcSystemCalls.ok: {
      const call = outboundTracker.get(relatedId);
      if (call !== undefined) {
        if (call.removeOnOk) {
          outboundTracker.remove(relatedId);
        }
        call.result.resolve(args[0]);
      }
      break;
    }
    case RpcSystemCalls.error: {
      const call = outboundTracker.remove(relatedId);
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
      inboundTracker.remove(relatedId);
      break;
    }
    case RpcSystemCalls.keepAlive: {
      // Remote keep-alive — nothing to do, just acknowledges the connection is alive
      break;
    }
  }
}
