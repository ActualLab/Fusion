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

  invalidate(conn: RpcConnection, relatedId: number): void {
    const msg = serializeMessage({
      Method: RpcSystemCalls.invalidate,
      RelatedId: relatedId,
    });
    conn.send(msg);
  }
}
