import { describe, it, expect, beforeEach, afterEach } from "vitest";
import {
  RpcHub,
  RpcClientPeer,
  RpcServerPeer,
  defineRpcService,
  createRpcClient,
  createMessageChannelPair,
} from "../src/index.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

interface ICalcService {
  add(a: number, b: number): Promise<number>;
  greet(name: string): Promise<string>;
  fail(): Promise<never>;
}

const CalcServiceDef = defineRpcService("CalcService", {
  add: { args: [0, 0] },
  greet: { args: [""] },
  fail: { args: [] },
});

describe("RPC End-to-End", () => {
  let serverHub: RpcHub;
  let clientHub: RpcHub;

  beforeEach(() => {
    serverHub = new RpcHub("server-hub");
    clientHub = new RpcHub("client-hub");

    // Register service on server
    serverHub.addService(CalcServiceDef, {
      add: (a: unknown, b: unknown) => (a as number) + (b as number),
      greet: (name: unknown) => `Hello, ${name}!`,
      fail: () => { throw new Error("intentional failure"); },
    });
  });

  afterEach(() => {
    serverHub.close();
    clientHub.close();
  });

  it("should call a remote method and get a result", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    const serverPeer = new RpcServerPeer("server", serverHub, serverConn);
    serverHub.addPeer(serverPeer);

    await delay(10); // Wait for connection

    const calc = createRpcClient<ICalcService>(clientPeer, CalcServiceDef);

    const result = await calc.add(3, 4);
    expect(result).toBe(7);
  });

  it("should call multiple methods", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    const serverPeer = new RpcServerPeer("server", serverHub, serverConn);
    serverHub.addPeer(serverPeer);

    await delay(10);

    const calc = createRpcClient<ICalcService>(clientPeer, CalcServiceDef);

    const sum = await calc.add(10, 20);
    expect(sum).toBe(30);

    const greeting = await calc.greet("World");
    expect(greeting).toBe("Hello, World!");
  });

  it("should propagate server errors", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    const serverPeer = new RpcServerPeer("server", serverHub, serverConn);
    serverHub.addPeer(serverPeer);

    await delay(10);

    const calc = createRpcClient<ICalcService>(clientPeer, CalcServiceDef);

    await expect(calc.fail()).rejects.toThrow("intentional failure");
  });

  it("should handle concurrent calls", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    const serverPeer = new RpcServerPeer("server", serverHub, serverConn);
    serverHub.addPeer(serverPeer);

    await delay(10);

    const calc = createRpcClient<ICalcService>(clientPeer, CalcServiceDef);

    const [r1, r2, r3] = await Promise.all([
      calc.add(1, 2),
      calc.add(10, 20),
      calc.greet("Fusion"),
    ]);

    expect(r1).toBe(3);
    expect(r2).toBe(30);
    expect(r3).toBe("Hello, Fusion!");
  });

  it("should detect disconnection", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    const serverPeer = new RpcServerPeer("server", serverHub, serverConn);
    serverHub.addPeer(serverPeer);

    await delay(10);
    expect(clientPeer.isConnected).toBe(true);

    let clientDisconnected = false;
    clientPeer.disconnected.add(() => { clientDisconnected = true; });

    clientPeer.close();
    await delay(10);
    expect(clientDisconnected).toBe(true);
  });

  it("should never throw from RpcConnection.send() on closed connection", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    const serverPeer = new RpcServerPeer("server", serverHub, serverConn);
    serverHub.addPeer(serverPeer);

    await delay(10);

    clientConn.close();
    await delay(10);

    // Should not throw
    expect(() => clientConn.send("test")).not.toThrow();
  });

  it("should handle noWait calls without registering in tracker", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const noWaitDef = defineRpcService("NoWaitService", {
      fire: { args: [""], noWait: true },
    });

    let received: string | undefined;
    serverHub.addService(noWaitDef, {
      fire: (msg: unknown) => { received = msg as string; },
    });

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    const serverPeer = new RpcServerPeer("server", serverHub, serverConn);
    serverHub.addPeer(serverPeer);

    await delay(10);

    // callNoWait should not throw and not register
    const trackerSizeBefore = clientPeer.outbound.size;
    clientPeer.callNoWait("NoWaitService.fire", ["hello"]);
    expect(clientPeer.outbound.size).toBe(trackerSizeBefore);

    await delay(50);
    expect(received).toBe("hello");
  });

  it("should work with addService/addClient unified API", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    const serverPeer = new RpcServerPeer("server", serverHub, serverConn);
    serverHub.addPeer(serverPeer);

    await delay(10);

    const calc = clientHub.addClient<ICalcService>(clientPeer, CalcServiceDef);

    const result = await calc.add(5, 7);
    expect(result).toBe(12);

    const greeting = await calc.greet("addClient");
    expect(greeting).toBe("Hello, addClient!");
  });

  it("should handle noWait call on disconnected peer silently", async () => {
    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    // Not connected — should not throw
    expect(() => clientPeer.callNoWait("CalcService.add", [1, 2])).not.toThrow();
  });
});
