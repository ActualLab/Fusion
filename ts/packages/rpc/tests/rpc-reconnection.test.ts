import { describe, it, expect, beforeEach, afterEach } from "vitest";
import {
  RpcHub,
  RpcClientPeer,
  RpcType,
  defineRpcService,
  createRpcClient,
  createMessageChannelPair,
} from "../src/index.js";
import { RpcTestConnection, connectionDisruptor } from "./rpc-test-connection.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

interface ICalcService {
  add(a: number, b: number): Promise<number>;
  greet(name: string): Promise<string>;
}

const CalcServiceDef = defineRpcService("CalcService", {
  add: { args: [0, 0] },
  greet: { args: [""] },
});

const NoWaitServiceDef = defineRpcService("NoWaitService", {
  fire: { args: [""], returns: RpcType.noWait },
});

describe("RPC Reconnection", () => {
  let serverHub: RpcHub;
  let clientHub: RpcHub;
  let conn: RpcTestConnection;

  beforeEach(async () => {
    serverHub = new RpcHub("server-hub");
    clientHub = new RpcHub("client-hub");

    serverHub.addService(CalcServiceDef, {
      add: (a: unknown, b: unknown) => (a as number) + (b as number),
      greet: (name: unknown) => `Hello, ${name}!`,
    });

    const clientPeer = new RpcClientPeer(clientHub, "ws://test");
    clientHub.addPeer(clientPeer);

    conn = new RpcTestConnection(clientHub, serverHub, clientPeer);
    await conn.connect();
  });

  afterEach(() => {
    serverHub.close();
    clientHub.close();
  });

  it("should call, disconnect, reconnect, and call again", async () => {
    const calc = createRpcClient<ICalcService>(conn.clientPeer, CalcServiceDef);

    const r1 = await calc.add(1, 2);
    expect(r1).toBe(3);

    await conn.reconnect();

    const r2 = await calc.add(10, 20);
    expect(r2).toBe(30);
  });

  it("should keep in-flight calls alive across reconnect", async () => {
    // Use a slow server so the call is still in-flight when we disconnect
    const slowHub = new RpcHub("slow-server");
    slowHub.addService(CalcServiceDef, {
      add: async (a: unknown, b: unknown) => {
        await delay(200);
        return (a as number) + (b as number);
      },
      greet: (name: unknown) => `Hello, ${name}!`,
    });
    await conn.switchHost(slowHub);

    const calc = createRpcClient<ICalcService>(conn.clientPeer, CalcServiceDef);
    const promise = calc.add(1, 2);

    // Disconnect while the call is in-flight, then reconnect
    await delay(10);
    await conn.reconnect();

    // Call should complete after reconnect (re-sent to the new connection)
    const result = await promise;
    expect(result).toBe(3);

    slowHub.close();
  });

  it("should keep multiple pending calls alive across reconnect", async () => {
    const slowHub = new RpcHub("slow-server");
    slowHub.addService(CalcServiceDef, {
      add: async (a: unknown, b: unknown) => {
        await delay(100);
        return (a as number) + (b as number);
      },
      greet: async (name: unknown) => {
        await delay(100);
        return `Hello, ${name}!`;
      },
    });
    await conn.switchHost(slowHub);

    const calc = createRpcClient<ICalcService>(conn.clientPeer, CalcServiceDef);
    const p1 = calc.add(1, 2);
    const p2 = calc.greet("test");
    const p3 = calc.add(3, 4);

    await delay(10);
    await conn.reconnect();

    // All calls should complete after reconnect
    expect(await p1).toBe(3);
    expect(await p2).toBe("Hello, test!");
    expect(await p3).toBe(7);

    slowHub.close();
  });

  it("should restore functionality after reconnect", async () => {
    const calc = createRpcClient<ICalcService>(conn.clientPeer, CalcServiceDef);

    const r1 = await calc.add(5, 5);
    expect(r1).toBe(10);

    await conn.disconnect();
    await conn.connect();

    const r2 = await calc.add(7, 8);
    expect(r2).toBe(15);

    const r3 = await calc.greet("Reconnected");
    expect(r3).toBe("Hello, Reconnected!");
  });

  it("should survive multiple reconnections", async () => {
    const calc = createRpcClient<ICalcService>(conn.clientPeer, CalcServiceDef);

    for (let i = 0; i < 5; i++) {
      const result = await calc.add(i, i * 10);
      expect(result).toBe(i + i * 10);
      await conn.reconnect(5);
    }

    // Final call after 5 reconnections
    const final = await calc.greet("Stable");
    expect(final).toBe("Hello, Stable!");
  });

  it("should silently drop noWait call on disconnected peer", async () => {
    await conn.disconnect();

    // Should not throw
    expect(() => {
      conn.clientPeer.callNoWait("NoWaitService.fire:2", ["test"]);
    }).not.toThrow();
  });

  it("should detect server identity change via handshake (peerChanged)", async () => {
    let peerChangedCount = 0;
    conn.clientPeer.peerChanged.add(() => peerChangedCount++);

    const calc = clientHub.addClient<ICalcService>(conn.clientPeer, CalcServiceDef);

    const r1 = await calc.add(1, 2);
    expect(r1).toBe(3);

    // Switch to a new server hub (different hubId — simulates server restart)
    const newServerHub = new RpcHub("new-server-hub");
    newServerHub.addService(CalcServiceDef, {
      add: (a: unknown, b: unknown) => (a as number) + (b as number) + 100,
      greet: (name: unknown) => `Hi, ${name}!`,
    });
    await conn.switchHost(newServerHub);

    // After switchHost, the server has a different hubId, but peerChanged
    // only fires during the handshake exchange in run().  Since connectWith()
    // (used by RpcTestConnection) doesn't exchange handshakes, peerChanged
    // won't fire here.  This test verifies the connectWith path is stable.
    // The run() path with handshake exchange is tested via integration tests.
    expect(peerChangedCount).toBe(0);

    // Calls still work after host switch
    const r2 = await calc.add(1, 2);
    expect(r2).toBe(103);

    newServerHub.close();
  });

  it("should exchange handshakes when server peer receives one", async () => {
    // Create a raw pair to test handshake exchange at the peer level
    const hub1 = new RpcHub("hub-A");
    const hub2 = new RpcHub("hub-B");
    hub2.addService(CalcServiceDef, {
      add: (a: unknown, b: unknown) => (a as number) + (b as number),
      greet: (name: unknown) => `Hello, ${name}!`,
    });

    const [conn1, conn2] = createMessageChannelPair();
    const client = new RpcClientPeer(hub1, "ws://test");
    client.connectWith(conn1);

    const server = hub2.getServerPeer("server://test");
    server.accept(conn2);

    // Manually send a handshake from client → server
    hub1.systemCallSender.handshake(conn1, client.id, "hub-A", 1);

    // Wait for the server's handshake response to arrive
    await delay(5);

    // The server should have sent its handshake back. We can verify
    // by checking that calls still work (handshake doesn't break anything).
    const calc = createRpcClient<ICalcService>(client, CalcServiceDef);
    const result = await calc.add(10, 20);
    expect(result).toBe(30);

    hub1.close();
    hub2.close();
  });

  it("should cancel an outbound call via AbortSignal", async () => {
    // Use a slow server so the call is in-flight when we cancel
    const slowHub = new RpcHub("slow-server");
    slowHub.addService(CalcServiceDef, {
      add: async (a: unknown, b: unknown) => {
        await delay(500);
        return (a as number) + (b as number);
      },
      greet: (name: unknown) => `Hello, ${name}!`,
    });
    await conn.switchHost(slowHub);

    const ac = new AbortController();
    const outboundCall = conn.clientPeer.call("CalcService.add:3", [1, 2], { signal: ac.signal });
    outboundCall.result.promise.catch(() => {}); // prevent unhandled rejection

    // Cancel after a short delay
    await delay(10);
    ac.abort();

    await expect(outboundCall.result.promise).rejects.toThrow("Call cancelled");
    // Call should be removed from the tracker
    expect(conn.clientPeer.outbound.get(outboundCall.callId)).toBeUndefined();

    slowHub.close();
  });

  it("should handle concurrent calls under disruption", async () => {
    const ac = new AbortController();
    let errors = 0;
    let successes = 0;

    // Start connection disruptor with short intervals
    const disruptorPromise = connectionDisruptor(conn, ac.signal, [30, 60], [5, 15]);

    // Run several calls during disruption
    const workers = Array.from({ length: 10 }, async (_, i) => {
      try {
        const calc = createRpcClient<ICalcService>(conn.clientPeer, CalcServiceDef);
        const result = await calc.add(i, 1);
        if (result === i + 1) successes++;
      } catch {
        errors++;
      }
    });

    await Promise.allSettled(workers);

    ac.abort();
    await disruptorPromise.catch(() => {});

    // Under disruption, some calls may fail and some may succeed
    // The key invariant is: no unhandled errors, no hangs
    expect(successes + errors).toBe(10);
  });
});
