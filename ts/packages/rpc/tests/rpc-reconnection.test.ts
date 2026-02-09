import { describe, it, expect, beforeEach, afterEach } from "vitest";
import {
  RpcHub,
  RpcClientPeer,
  defineRpcService,
  createRpcClient,
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
  fire: { args: [""], noWait: true },
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

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
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

  it("should reject in-flight calls on disconnect", async () => {
    const calc = createRpcClient<ICalcService>(conn.clientPeer, CalcServiceDef);

    // Start a call but disconnect before it completes
    // We need a slow service for this
    const slowHub = new RpcHub("slow-server");
    slowHub.addService(CalcServiceDef, {
      add: async (a: unknown, b: unknown) => {
        await delay(500);
        return (a as number) + (b as number);
      },
      greet: (name: unknown) => `Hello, ${name}!`,
    });

    await conn.switchHost(slowHub);

    const promise = calc.add(1, 2);
    promise.catch(() => {}); // prevent unhandled rejection warning

    // Disconnect while the call is in-flight
    await delay(10);
    await conn.disconnect();

    await expect(promise).rejects.toThrow("Connection closed");

    slowHub.close();
  });

  it("should reject all pending calls on disconnect", async () => {
    // Switch to a server that never responds (slow)
    const slowHub = new RpcHub("slow-server");
    slowHub.addService(CalcServiceDef, {
      add: async () => { await delay(10_000); return 0; },
      greet: async () => { await delay(10_000); return ""; },
    });
    await conn.switchHost(slowHub);

    const calc = createRpcClient<ICalcService>(conn.clientPeer, CalcServiceDef);

    const p1 = calc.add(1, 2);
    const p2 = calc.greet("test");
    const p3 = calc.add(3, 4);
    // Prevent unhandled rejection warnings
    p1.catch(() => {});
    p2.catch(() => {});
    p3.catch(() => {});

    await delay(10);
    await conn.disconnect();

    await expect(p1).rejects.toThrow("Connection closed");
    await expect(p2).rejects.toThrow("Connection closed");
    await expect(p3).rejects.toThrow("Connection closed");

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
      conn.clientPeer.callNoWait("NoWaitService.fire", ["test"]);
    }).not.toThrow();
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
