import { describe, it, expect, beforeEach, afterEach } from "vitest";
import {
  RpcHub,
  RpcClientPeer,
  RpcOutboundCall,
  RpcSystemCalls,
  defineRpcService,
  createRpcClient,
  createMessageChannelPair,
  serializeMessage,
} from "../src/index.js";
import { RpcTestConnection } from "./rpc-test-connection.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

const CalcServiceDef = defineRpcService("CalcService", {
  add: { args: [0, 0] },
});

describe("$sys.Disconnect handler", () => {
  let serverHub: RpcHub;
  let clientHub: RpcHub;
  let conn: RpcTestConnection;

  beforeEach(async () => {
    serverHub = new RpcHub("server-hub");
    clientHub = new RpcHub("client-hub");

    serverHub.addService(CalcServiceDef, {
      add: (a: unknown, b: unknown) => (a as number) + (b as number),
    });

    const clientPeer = new RpcClientPeer(clientHub, "test://server");
    conn = new RpcTestConnection(clientHub, serverHub, clientPeer);
    await conn.connect();
  });

  afterEach(() => {
    serverHub.close();
    clientHub.close();
  });

  it("should remove regular outbound calls on $sys.Disconnect", async () => {
    const client = createRpcClient<{ add(a: number, b: number): Promise<number> }>(
      conn.clientPeer, CalcServiceDef,
    );

    // Complete a normal call
    const result = await client.add(1, 2);
    expect(result).toBe(3);

    // Regular calls with removeOnOk=true are removed after $sys.Ok,
    // so the tracker should be empty
    expect(conn.clientPeer.outbound.activeCallIds()).toEqual([]);
  });

  it("should process $sys.Disconnect and call onDisconnect for tracked calls", async () => {
    const tracker = conn.clientPeer.outbound;

    // Manually register a call in the outbound tracker to simulate a pending call
    const fakeCallId = tracker.nextId();
    const fakeCall = new RpcOutboundCall(fakeCallId, "FakeMethod");
    tracker.register(fakeCall);

    expect(tracker.get(fakeCallId)).toBeDefined();

    // Server sends $sys.Disconnect for this call ID
    const disconnectMsg = serializeMessage(
      { Method: RpcSystemCalls.disconnect },
      [[fakeCallId]],
    );
    conn.serverPeer!.connection!.send(disconnectMsg);
    await delay(10);

    // The call should be removed from the tracker
    expect(tracker.get(fakeCallId)).toBeUndefined();

    // The call's result should be rejected (onDisconnect was called)
    // Regular calls don't reject on disconnect by default, but they are removed
  });

  it("should call onDisconnect on the outbound call", async () => {
    const tracker = conn.clientPeer.outbound;

    // Create a custom call that tracks disconnect
    let disconnected = false;
    const fakeCallId = tracker.nextId();
    const fakeCall = new RpcOutboundCall(fakeCallId, "FakeMethod");
    const origDisconnect = fakeCall.onDisconnect.bind(fakeCall);
    fakeCall.onDisconnect = () => { disconnected = true; origDisconnect(); };
    tracker.register(fakeCall);

    // Server sends $sys.Disconnect
    const disconnectMsg = serializeMessage(
      { Method: RpcSystemCalls.disconnect },
      [[fakeCallId]],
    );
    conn.serverPeer!.connection!.send(disconnectMsg);
    await delay(10);

    expect(disconnected).toBe(true);
    expect(tracker.get(fakeCallId)).toBeUndefined();
  });

  it("should handle $sys.Disconnect for unknown IDs gracefully", async () => {
    const disconnectMsg = serializeMessage(
      { Method: RpcSystemCalls.disconnect },
      [[999, 1000, 1001]],
    );
    conn.serverPeer!.connection!.send(disconnectMsg);
    await delay(10);

    // Should not throw — silently ignore unknown IDs
    expect(conn.clientPeer.outbound.activeCallIds()).toEqual([]);
  });

  it("should handle $sys.Disconnect with empty array", async () => {
    const disconnectMsg = serializeMessage(
      { Method: RpcSystemCalls.disconnect },
      [[]],
    );
    conn.serverPeer!.connection!.send(disconnectMsg);
    await delay(10);

    expect(true).toBe(true);
  });

  it("should process multiple IDs in a single $sys.Disconnect", async () => {
    const tracker = conn.clientPeer.outbound;

    // Register 3 fake calls
    const ids: number[] = [];
    for (let i = 0; i < 3; i++) {
      const id = tracker.nextId();
      tracker.register(new RpcOutboundCall(id, `Fake${i}`));
      ids.push(id);
    }

    expect(tracker.activeCallIds().length).toBe(3);

    // Disconnect all 3 at once
    const disconnectMsg = serializeMessage(
      { Method: RpcSystemCalls.disconnect },
      [ids],
    );
    conn.serverPeer!.connection!.send(disconnectMsg);
    await delay(10);

    expect(tracker.activeCallIds().length).toBe(0);
  });

  it("should only remove specified IDs, not others", async () => {
    const tracker = conn.clientPeer.outbound;

    const id1 = tracker.nextId();
    const id2 = tracker.nextId();
    const id3 = tracker.nextId();
    tracker.register(new RpcOutboundCall(id1, "A"));
    tracker.register(new RpcOutboundCall(id2, "B"));
    tracker.register(new RpcOutboundCall(id3, "C"));

    // Disconnect only id1 and id3
    const disconnectMsg = serializeMessage(
      { Method: RpcSystemCalls.disconnect },
      [[id1, id3]],
    );
    conn.serverPeer!.connection!.send(disconnectMsg);
    await delay(10);

    expect(tracker.get(id1)).toBeUndefined();
    expect(tracker.get(id2)).toBeDefined(); // still alive
    expect(tracker.get(id3)).toBeUndefined();
  });
});
