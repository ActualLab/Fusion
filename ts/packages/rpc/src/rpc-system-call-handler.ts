import { RpcSystemCalls, type RpcMessage } from "./rpc-message.js";
import type { RpcOutboundCallTracker, RpcOutboundComputeCall } from "./rpc-call-tracker.js";

/** Handles an incoming system call message and dispatches to the appropriate tracker. */
export function handleSystemCall(
  message: RpcMessage,
  args: unknown[],
  outboundTracker: RpcOutboundCallTracker,
): void {
  const method = message.Method;
  const relatedId = message.RelatedId ?? 0;

  switch (method) {
    case RpcSystemCalls.ok: {
      // For compute calls, keep in tracker — they stay active until invalidated
      const call = outboundTracker.get(relatedId);
      if (call !== undefined) {
        if (!("whenInvalidated" in call)) {
          outboundTracker.remove(relatedId);
        }
        call.result.resolve(args[0]);
      }
      break;
    }
    case RpcSystemCalls.error: {
      const call = outboundTracker.remove(relatedId);
      if (call !== undefined) {
        const errorInfo = args[0] as { Message?: string } | undefined;
        call.result.reject(new Error(errorInfo?.Message ?? "RPC error"));
      }
      break;
    }
    case RpcSystemCalls.cancel: {
      // Server cancelled our call
      const call = outboundTracker.remove(relatedId);
      if (call !== undefined) {
        call.result.reject(new Error("Call cancelled by remote peer."));
      }
      break;
    }
    case RpcSystemCalls.invalidate: {
      // Server invalidated a compute call — remove from tracker and resolve
      const call = outboundTracker.remove(relatedId);
      if (call !== undefined && "whenInvalidated" in call) {
        (call as RpcOutboundComputeCall).whenInvalidated.resolve();
      }
      break;
    }
    case RpcSystemCalls.keepAlive: {
      // Server keep-alive — nothing to do, just acknowledges the connection is alive
      break;
    }
  }
}
