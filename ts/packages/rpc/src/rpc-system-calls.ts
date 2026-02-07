import { RpcSystemCalls, type RpcMessage } from "./rpc-message.js";
import type { RpcOutboundCallTracker, RpcOutboundComputeCall } from "./rpc-call-tracker.js";
import { serializeMessage } from "./rpc-serialization.js";
import type { RpcConnection } from "./rpc-connection.js";

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

/** Sends a $sys.Ok response. */
export function sendOk(conn: RpcConnection, relatedId: number, result: unknown): void {
  const msg = serializeMessage({ Method: RpcSystemCalls.ok, RelatedId: relatedId }, [result]);
  conn.send(msg);
}

/** Sends a $sys.Error response. */
export function sendError(conn: RpcConnection, relatedId: number, error: unknown): void {
  const message = error instanceof Error ? error.message : String(error);
  const msg = serializeMessage(
    { Method: RpcSystemCalls.error, RelatedId: relatedId },
    [{ Message: message }],
  );
  conn.send(msg);
}

/** Sends a $sys.KeepAlive message with active call IDs. */
export function sendKeepAlive(conn: RpcConnection, activeCallIds: number[]): void {
  const msg = serializeMessage({ Method: RpcSystemCalls.keepAlive }, [activeCallIds]);
  conn.send(msg);
}

/** Sends a $sys.Handshake message. */
export function sendHandshake(
  conn: RpcConnection,
  peerId: string,
  hubId: string,
  index: number,
): void {
  const msg = serializeMessage({ Method: RpcSystemCalls.handshake }, [{
    RemotePeerId: peerId,
    RemoteApiVersionSet: null,
    RemoteHubId: hubId,
    ProtocolVersion: 2,
    Index: index,
  }]);
  conn.send(msg);
}
