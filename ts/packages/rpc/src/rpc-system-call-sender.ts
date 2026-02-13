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

import { RpcSystemCalls } from "./rpc-message.js";
import { serializeMessage } from "./rpc-serialization.js";
import type { RpcConnection } from "./rpc-connection.js";

/** Sends system RPC messages — like .NET's RpcSystemCallSender. */
export class RpcSystemCallSender {
  handshake(conn: RpcConnection, peerId: string, hubId: string, index: number): void {
    const msg = serializeMessage({ Method: RpcSystemCalls.handshake }, [{
      RemotePeerId: peerId,
      RemoteApiVersionSet: null,
      RemoteHubId: hubId,
      ProtocolVersion: 2,
      Index: index,
    }]);
    conn.send(msg);
  }

  ok(conn: RpcConnection, relatedId: number, result: unknown): void {
    const msg = serializeMessage({ Method: RpcSystemCalls.ok, RelatedId: relatedId }, [result]);
    conn.send(msg);
  }

  error(conn: RpcConnection, relatedId: number, error: unknown): void {
    const message = error instanceof Error ? error.message : String(error);
    const msg = serializeMessage(
      { Method: RpcSystemCalls.error, RelatedId: relatedId },
      [{ Message: message }],
    );
    conn.send(msg);
  }

  cancel(conn: RpcConnection, relatedId: number): void {
    const msg = serializeMessage({ Method: RpcSystemCalls.cancel, RelatedId: relatedId });
    conn.send(msg);
  }

  keepAlive(conn: RpcConnection, activeCallIds: number[]): void {
    const msg = serializeMessage({ Method: RpcSystemCalls.keepAlive }, [activeCallIds]);
    conn.send(msg);
  }

}
