import { describe, it, expect, beforeEach, afterEach } from "vitest";
import {
  RpcHub,
  RpcClientPeer,
  RpcType,
  RpcSystemCalls,
  defineRpcService,
  createRpcClient,
  createMessageChannelPair,
  serializeMessage,
} from "../src/index.js";
import { RpcStream, parseStreamRef } from "../src/rpc-stream.js";
import { RpcTestConnection } from "./rpc-test-connection.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

const StreamServiceDef = defineRpcService("StreamService", {
  getNumbers: { args: [0], returns: RpcType.stream },
});

describe("RpcStream disconnect on $sys.Disconnect (PR #5)", () => {
  let serverHub: RpcHub;
  let clientHub: RpcHub;
  let conn: RpcTestConnection;

  beforeEach(async () => {
    serverHub = new RpcHub("server-hub");
    clientHub = new RpcHub("client-hub");

    serverHub.addService(StreamServiceDef, {
      getNumbers: async function*(count: unknown) {
        for (let i = 0; i < (count as number); i++) {
          yield i;
          await delay(50);
        }
      },
    });

    const clientPeer = new RpcClientPeer(clientHub, "test://server");
    conn = new RpcTestConnection(clientHub, serverHub, clientPeer);
    await conn.connect();
  });

  afterEach(() => {
    serverHub.close();
    clientHub.close();
  });

  it("$sys.Disconnect for stream ID should disconnect the stream", async () => {
    // Manually create and register a stream to test disconnect
    const ref = { hostId: "test-host", localId: 999, ackPeriod: 10, ackAdvance: 5, allowReconnect: true };
    const stream = new RpcStream<number>(ref, conn.clientPeer);
    conn.clientPeer.remoteObjects.register(stream);

    expect(conn.clientPeer.remoteObjects.get(999)).toBeDefined();

    // Server sends $sys.Disconnect for the stream's localId
    const disconnectMsg = serializeMessage(
      { Method: RpcSystemCalls.disconnect },
      [[999]],
    );
    conn.serverPeer!.connection!.send(disconnectMsg);
    await delay(10);

    // Stream should be disconnected and unregistered
    expect(conn.clientPeer.remoteObjects.get(999)).toBeUndefined();
  });

  it("disconnected stream should terminate for-await with error", async () => {
    const ref = { hostId: "test-host", localId: 888, ackPeriod: 10, ackAdvance: 5, allowReconnect: true };
    const stream = new RpcStream<number>(ref, conn.clientPeer);
    conn.clientPeer.remoteObjects.register(stream);

    // Push some items
    stream.onItem(0, 42);
    stream.onItem(1, 43);

    let error: Error | null = null;
    const items: number[] = [];

    // Start consuming
    const consumePromise = (async () => {
      try {
        for await (const item of stream) {
          items.push(item as number);
        }
      } catch (e) {
        error = e as Error;
      }
    })();

    await delay(10);
    expect(items).toEqual([42, 43]);

    // Server disconnects the stream
    const disconnectMsg = serializeMessage(
      { Method: RpcSystemCalls.disconnect },
      [[888]],
    );
    conn.serverPeer!.connection!.send(disconnectMsg);
    await delay(10);

    await consumePromise;
    expect(error).not.toBeNull();
    expect(error!.message).toContain("disconnected");
  });

  it("$sys.Disconnect should handle mix of call IDs and stream IDs", async () => {
    // Register a stream
    const ref = { hostId: "test-host", localId: 777, ackPeriod: 10, ackAdvance: 5, allowReconnect: true };
    const stream = new RpcStream<number>(ref, conn.clientPeer);
    conn.clientPeer.remoteObjects.register(stream);

    // Register a regular outbound call
    const { RpcOutboundCall } = await import("../src/rpc-call-tracker.js");
    const tracker = conn.clientPeer.outbound;
    const callId = tracker.nextId();
    const call = new RpcOutboundCall(callId, "SomeMethod");
    tracker.register(call);

    expect(conn.clientPeer.remoteObjects.get(777)).toBeDefined();
    expect(tracker.get(callId)).toBeDefined();

    // Disconnect both
    const disconnectMsg = serializeMessage(
      { Method: RpcSystemCalls.disconnect },
      [[777, callId]],
    );
    conn.serverPeer!.connection!.send(disconnectMsg);
    await delay(10);

    expect(conn.clientPeer.remoteObjects.get(777)).toBeUndefined();
    expect(tracker.get(callId)).toBeUndefined();
  });

  it("stream disconnect should unregister from remoteObjects", () => {
    const ref = { hostId: "test-host", localId: 666, ackPeriod: 10, ackAdvance: 5, allowReconnect: true };
    const stream = new RpcStream<number>(ref, conn.clientPeer);
    conn.clientPeer.remoteObjects.register(stream);

    expect(conn.clientPeer.remoteObjects.get(666)).toBeDefined();

    stream.disconnect();

    expect(conn.clientPeer.remoteObjects.get(666)).toBeUndefined();
  });

  it("KeepAlive should include stream IDs so server can track them", () => {
    const ref = { hostId: "test-host", localId: 555, ackPeriod: 10, ackAdvance: 5, allowReconnect: true };
    const stream = new RpcStream<number>(ref, conn.clientPeer);
    conn.clientPeer.remoteObjects.register(stream);

    const remoteIds = conn.clientPeer.remoteObjects.activeIds();
    expect(remoteIds).toContain(555);
  });
});
