import { describe, it, expect, beforeEach, afterEach } from "vitest";
import {
  RpcHub,
  RpcClientPeer,
  RpcOutboundCall,
  defineRpcService,
  createRpcClient,
} from "../src/index.js";
import { RpcTestConnection } from "./rpc-test-connection.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

const CalcServiceDef = defineRpcService("CalcService", {
  add: { args: [0, 0] },
});

describe("_reconnect() handling of compute calls (PR #2)", () => {
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

  it("should self-invalidate completed compute calls on reconnect", async () => {
    const tracker = conn.clientPeer.outbound;

    // Simulate a completed (stage-3) compute call
    const id = tracker.nextId();
    const call = new RpcOutboundCall(id, "ComputeMethod");
    (call as any).removeOnOk = false;
    call.result.resolve("cached-value"); // stage-3: result resolved, awaiting invalidation
    tracker.register(call);

    let disconnected = false;
    const origDisconnect = call.onDisconnect.bind(call);
    call.onDisconnect = () => { disconnected = true; origDisconnect(); };

    expect(tracker.get(id)).toBeDefined();

    // Reconnect
    await conn.reconnect();

    // Compute call should be self-invalidated and removed
    expect(disconnected).toBe(true);
    expect(tracker.get(id)).toBeUndefined();
  });

  it("should self-invalidate in-flight compute calls on reconnect", async () => {
    const tracker = conn.clientPeer.outbound;

    // Simulate an in-flight compute call (result NOT yet resolved)
    const id = tracker.nextId();
    const call = new RpcOutboundCall(id, "ComputeMethod");
    (call as any).removeOnOk = false;
    // Don't resolve result — this is in-flight
    tracker.register(call);

    let disconnected = false;
    const origDisconnect = call.onDisconnect.bind(call);
    call.onDisconnect = () => { disconnected = true; origDisconnect(); };

    expect(tracker.get(id)).toBeDefined();
    expect(call.result.isCompleted).toBe(false);

    // Reconnect
    await conn.reconnect();

    // In-flight compute call should ALSO be self-invalidated
    expect(disconnected).toBe(true);
    expect(tracker.get(id)).toBeUndefined();
  });

  it("should not disconnect regular in-flight calls on reconnect", async () => {
    const tracker = conn.clientPeer.outbound;

    // Simulate a regular in-flight call with valid serialized message
    const { serializeMessage } = await import("../src/rpc-serialization.js");
    const id = tracker.nextId();
    const call = new RpcOutboundCall(id, "CalcService.add:3");
    call.serializedMessage = serializeMessage(
      { Method: "CalcService.add:3", RelatedId: id, CallType: 0 },
      [1, 2],
    );
    tracker.register(call);

    let disconnected = false;
    call.onDisconnect = () => { disconnected = true; };

    expect(call.removeOnOk).toBe(true);

    await conn.reconnect();

    // Regular call should NOT be disconnected (it gets re-sent)
    expect(disconnected).toBe(false);
  });

  it("should handle mix of compute and regular calls on reconnect", async () => {
    const tracker = conn.clientPeer.outbound;

    // Completed compute call
    const computeCompletedId = tracker.nextId();
    const computeCompleted = new RpcOutboundCall(computeCompletedId, "C1");
    (computeCompleted as any).removeOnOk = false;
    computeCompleted.result.resolve("done");
    tracker.register(computeCompleted);

    // In-flight compute call
    const computeInflightId = tracker.nextId();
    const computeInflight = new RpcOutboundCall(computeInflightId, "C2");
    (computeInflight as any).removeOnOk = false;
    tracker.register(computeInflight);

    let computeCompletedDisconnected = false;
    let computeInflightDisconnected = false;
    computeCompleted.onDisconnect = () => { computeCompletedDisconnected = true; };
    computeInflight.onDisconnect = () => { computeInflightDisconnected = true; };

    await conn.reconnect();

    // Both compute calls should be self-invalidated
    expect(computeCompletedDisconnected).toBe(true);
    expect(computeInflightDisconnected).toBe(true);
    expect(tracker.get(computeCompletedId)).toBeUndefined();
    expect(tracker.get(computeInflightId)).toBeUndefined();
  });

  it("should handle empty outbound tracker on reconnect", async () => {
    expect(conn.clientPeer.outbound.activeCallIds().length).toBe(0);

    // Should not throw
    await conn.reconnect();

    expect(conn.clientPeer.outbound.activeCallIds().length).toBe(0);
  });
});
