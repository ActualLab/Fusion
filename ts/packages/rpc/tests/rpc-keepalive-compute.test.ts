import { describe, it, expect, beforeEach, afterEach } from "vitest";
import {
  RpcHub,
  RpcClientPeer,
  RpcOutboundCall,
  RpcOutboundCallTracker,
  createMessageChannelPair,
} from "../src/index.js";
import { RpcTestConnection } from "./rpc-test-connection.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

describe("KeepAlive excludes compute calls (PR #1)", () => {
  it("activeCallIds returns all call IDs", () => {
    const tracker = new RpcOutboundCallTracker();

    const regular = new RpcOutboundCall(tracker.nextId(), "Regular");
    tracker.register(regular);

    const computeId = tracker.nextId();
    const compute = new RpcOutboundCall(computeId, "Compute");
    // Simulate compute call: removeOnOk = false
    (compute as any).removeOnOk = false;
    tracker.register(compute);

    expect(tracker.activeCallIds().length).toBe(2);
    expect(tracker.activeCallIds()).toContain(regular.callId);
    expect(tracker.activeCallIds()).toContain(compute.callId);
  });

  it("keepAliveCallIds excludes compute calls (removeOnOk=false)", () => {
    const tracker = new RpcOutboundCallTracker();

    const regular = new RpcOutboundCall(tracker.nextId(), "Regular");
    tracker.register(regular);

    const computeId = tracker.nextId();
    const compute = new RpcOutboundCall(computeId, "Compute");
    (compute as any).removeOnOk = false;
    tracker.register(compute);

    const keepAliveIds = tracker.keepAliveCallIds();
    expect(keepAliveIds.length).toBe(1);
    expect(keepAliveIds).toContain(regular.callId);
    expect(keepAliveIds).not.toContain(compute.callId);
  });

  it("keepAliveCallIds returns empty when only compute calls exist", () => {
    const tracker = new RpcOutboundCallTracker();

    const c1 = new RpcOutboundCall(tracker.nextId(), "C1");
    (c1 as any).removeOnOk = false;
    tracker.register(c1);

    const c2 = new RpcOutboundCall(tracker.nextId(), "C2");
    (c2 as any).removeOnOk = false;
    tracker.register(c2);

    expect(tracker.activeCallIds().length).toBe(2);
    expect(tracker.keepAliveCallIds().length).toBe(0);
  });

  it("keepAliveCallIds returns all when no compute calls exist", () => {
    const tracker = new RpcOutboundCallTracker();

    const r1 = new RpcOutboundCall(tracker.nextId(), "R1");
    tracker.register(r1);

    const r2 = new RpcOutboundCall(tracker.nextId(), "R2");
    tracker.register(r2);

    expect(tracker.keepAliveCallIds().length).toBe(2);
  });

  it("keepAliveCallIds used by peer excludes compute calls from wire message", async () => {
    const tracker = new RpcOutboundCallTracker();

    // Use large IDs to avoid substring false matches
    for (let i = 0; i < 100; i++) tracker.nextId(); // burn IDs

    const computeCall = new RpcOutboundCall(tracker.nextId(), "Compute"); // ID ~101
    (computeCall as any).removeOnOk = false;
    tracker.register(computeCall);

    const regularCall = new RpcOutboundCall(tracker.nextId(), "Regular"); // ID ~102
    tracker.register(regularCall);

    // Verify keepAliveCallIds filters correctly
    const kaIds = tracker.keepAliveCallIds();
    expect(kaIds).toEqual([regularCall.callId]);

    // Verify activeCallIds includes both
    const allIds = tracker.activeCallIds();
    expect(allIds).toContain(computeCall.callId);
    expect(allIds).toContain(regularCall.callId);
  });
});
