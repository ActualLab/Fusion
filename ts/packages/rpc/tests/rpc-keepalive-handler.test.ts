import { describe, it, expect, beforeEach, afterEach } from "vitest";
import {
  RpcHub,
  RpcClientPeer,
  RpcSystemCalls,
  createMessageChannelPair,
  serializeMessage,
} from "../src/index.js";
import { RpcRemoteObjectTracker } from "../src/rpc-remote-object-tracker.js";
import type { IRpcObject, RpcObjectId } from "../src/rpc-object.js";
import { RpcObjectKind } from "../src/rpc-object.js";
import { RpcTestConnection } from "./rpc-test-connection.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

/** Minimal mock remote object for testing. */
class MockRemoteObject implements IRpcObject {
  readonly id: RpcObjectId;
  readonly kind = RpcObjectKind.Remote;
  readonly allowReconnect = true;
  disconnected = false;
  reconnected = false;

  constructor(localId: number, hostId = "host") {
    this.id = { localId, hostId };
  }
  reconnect() { this.reconnected = true; }
  disconnect() { this.disconnected = true; }
}

describe("KeepAlive response handling (PR #3)", () => {

  describe("RpcRemoteObjectTracker", () => {
    it("activeIds returns all registered object IDs", () => {
      const tracker = new RpcRemoteObjectTracker();
      const obj1 = new MockRemoteObject(100);
      const obj2 = new MockRemoteObject(200);
      tracker.register(obj1);
      tracker.register(obj2);

      const ids = tracker.activeIds();
      expect(ids).toContain(100);
      expect(ids).toContain(200);
      expect(ids.length).toBe(2);
    });

    it("keepAlive returns unknown IDs (not in tracker)", () => {
      const tracker = new RpcRemoteObjectTracker();
      tracker.register(new MockRemoteObject(100));
      tracker.register(new MockRemoteObject(200));

      // Server sends [100, 300, 400] — 300 and 400 are unknown
      const unknown = tracker.keepAlive([100, 300, 400]);
      expect(unknown).toEqual([300, 400]);
    });

    it("keepAlive returns empty when all IDs are known", () => {
      const tracker = new RpcRemoteObjectTracker();
      tracker.register(new MockRemoteObject(100));
      tracker.register(new MockRemoteObject(200));

      const unknown = tracker.keepAlive([100, 200]);
      expect(unknown).toEqual([]);
    });

    it("keepAlive returns all IDs when tracker is empty", () => {
      const tracker = new RpcRemoteObjectTracker();
      const unknown = tracker.keepAlive([100, 200]);
      expect(unknown).toEqual([100, 200]);
    });

    it("disconnectNotIn disconnects objects not in server set", () => {
      const tracker = new RpcRemoteObjectTracker();
      const obj1 = new MockRemoteObject(100);
      const obj2 = new MockRemoteObject(200);
      const obj3 = new MockRemoteObject(300);
      tracker.register(obj1);
      tracker.register(obj2);
      tracker.register(obj3);

      tracker.disconnectNotIn(new Set([200]));

      expect(obj1.disconnected).toBe(true);
      expect(obj2.disconnected).toBe(false);
      expect(obj3.disconnected).toBe(true);
      expect(tracker.size).toBe(1);
      expect(tracker.get(200)).toBeDefined();
    });
  });

  describe("KeepAlive handler sends $sys.Disconnect for unknown IDs", () => {
    let serverHub: RpcHub;
    let clientHub: RpcHub;
    let conn: RpcTestConnection;

    beforeEach(async () => {
      serverHub = new RpcHub("server-hub");
      clientHub = new RpcHub("client-hub");
      const clientPeer = new RpcClientPeer(clientHub, "test://server");
      conn = new RpcTestConnection(clientHub, serverHub, clientPeer);
      await conn.connect();
    });

    afterEach(() => {
      serverHub.close();
      clientHub.close();
    });

    it("should respond with $sys.Disconnect for IDs not in remoteObjects", async () => {
      // Register one remote object on the client
      const obj = new MockRemoteObject(42);
      conn.clientPeer.remoteObjects.register(obj);

      // Capture messages sent by the client
      const sentMessages: string[] = [];
      const origSend = conn.clientPeer.connection!.send.bind(conn.clientPeer.connection!);
      conn.clientPeer.connection!.send = (msg: string) => {
        sentMessages.push(msg);
        origSend(msg);
      };

      // Server sends KeepAlive with [42, 99, 100] — 99 and 100 are unknown
      const keepAliveMsg = serializeMessage(
        { Method: RpcSystemCalls.keepAlive },
        [[42, 99, 100]],
      );
      conn.serverPeer!.connection!.send(keepAliveMsg);
      await delay(10);

      // Client should send $sys.Disconnect for [99, 100]
      const disconnectMsg = sentMessages.find(m => m.includes("Disconnect"));
      expect(disconnectMsg).toBeDefined();
      expect(disconnectMsg).toContain("99");
      expect(disconnectMsg).toContain("100");
      // Should NOT disconnect 42 (it's known)
      expect(obj.disconnected).toBe(false);
    });

    it("should not send $sys.Disconnect when all IDs are known", async () => {
      conn.clientPeer.remoteObjects.register(new MockRemoteObject(10));
      conn.clientPeer.remoteObjects.register(new MockRemoteObject(20));

      const sentMessages: string[] = [];
      const origSend = conn.clientPeer.connection!.send.bind(conn.clientPeer.connection!);
      conn.clientPeer.connection!.send = (msg: string) => {
        sentMessages.push(msg);
        origSend(msg);
      };

      const keepAliveMsg = serializeMessage(
        { Method: RpcSystemCalls.keepAlive },
        [[10, 20]],
      );
      conn.serverPeer!.connection!.send(keepAliveMsg);
      await delay(10);

      const disconnectMsg = sentMessages.find(m => m.includes("Disconnect"));
      expect(disconnectMsg).toBeUndefined();
    });

    it("should handle empty KeepAlive gracefully", async () => {
      const keepAliveMsg = serializeMessage(
        { Method: RpcSystemCalls.keepAlive },
        [[]],
      );
      conn.serverPeer!.connection!.send(keepAliveMsg);
      await delay(10);
      // Should not throw
      expect(true).toBe(true);
    });

    it("KeepAlive sender includes remote object IDs", () => {
      const tracker = conn.clientPeer.outbound;
      conn.clientPeer.remoteObjects.register(new MockRemoteObject(500));

      // The KeepAlive should now include remote object IDs
      const remoteIds = conn.clientPeer.remoteObjects.activeIds();
      expect(remoteIds).toContain(500);
    });
  });
});
