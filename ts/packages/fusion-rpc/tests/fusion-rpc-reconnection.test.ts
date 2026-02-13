import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { AsyncContext } from "@actuallab/core";
import {
  Computed,
  MutableState,
  computeMethod,
} from "@actuallab/fusion";
import {
  RpcClientPeer,
  rpcService,
  rpcMethod,
  defineRpcService,
  createRpcClient,
  createMessageChannelPair,
} from "@actuallab/rpc";
import { FusionHub, RpcOutboundComputeCall, defineComputeService } from "../src/index.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

// Decorator-based contract class
@rpcService("CounterService")
class ICounterService {
  @computeMethod @rpcMethod()
  async getCount(key: string): Promise<number> { return undefined!; }

  @computeMethod @rpcMethod()
  async getDoubled(key: string): Promise<number> { return undefined!; }
}

const MutationServiceDef = defineRpcService("MutationService", {
  setCount: { args: ["", 0], noWait: true },
});

function createServerHub(store: Map<string, MutableState<number>>): FusionHub {
  const hub = new FusionHub();

  function getState(key: string): MutableState<number> {
    let s = store.get(key);
    if (s === undefined) {
      s = new MutableState(0);
      store.set(key, s);
    }
    return s;
  }

  hub.addService(ICounterService, {
    async getCount(key: unknown): Promise<number> {
      return getState(key as string).use();
    },
    async getDoubled(key: unknown): Promise<number> {
      return getState(key as string).use() * 2;
    },
  });

  hub.addService(MutationServiceDef, {
    setCount(key: unknown, value: unknown) {
      getState(key as string).set(value as number);
    },
  });

  return hub;
}

describe("Fusion RPC Reconnection", () => {
  let storeA: Map<string, MutableState<number>>;
  let serverHubA: FusionHub;
  let clientHub: FusionHub;

  beforeEach(() => {
    AsyncContext.current = undefined;
    storeA = new Map();
    serverHubA = createServerHub(storeA);
    clientHub = new FusionHub("client");
  });

  afterEach(() => {
    serverHubA.close();
    clientHub.close();
  });

  it("should NOT invalidate stage-3 compute calls on disconnect", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);
    serverHubA.acceptRpcConnection(serverConn);
    await delay(10);

    // Make a compute call
    const outboundCall = clientPeer.call(
      "CounterService.getCount:2", ["x"],
      { callTypeId: 1, outboundCallFactory: (id: number, m: string) => new RpcOutboundComputeCall(id, m) },
    ) as RpcOutboundComputeCall;

    const result = await outboundCall.result.promise;
    expect(result).toBe(0);

    // Disconnect — stage-3 compute call should survive
    clientConn.close();
    await delay(10);

    expect(outboundCall.whenInvalidated.isCompleted).toBe(false);
  });

  it("should invalidate stage-3 compute calls on reconnect", async () => {
    const [clientConn1, serverConn1] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn1);
    clientHub.addPeer(clientPeer);
    serverHubA.acceptRpcConnection(serverConn1);
    await delay(10);

    // Make a compute call
    const outboundCall = clientPeer.call(
      "CounterService.getCount:2", ["x"],
      { callTypeId: 1, outboundCallFactory: (id: number, m: string) => new RpcOutboundComputeCall(id, m) },
    ) as RpcOutboundComputeCall;

    const result = await outboundCall.result.promise;
    expect(result).toBe(0);

    // Disconnect — stage-3 compute call survives
    clientConn1.close();
    await delay(10);
    expect(outboundCall.whenInvalidated.isCompleted).toBe(false);

    // Reconnect — triggers invalidation
    const [clientConn2, serverConn2] = createMessageChannelPair();
    clientPeer.connectWith(clientConn2);
    serverHubA.acceptRpcConnection(serverConn2);
    await delay(10);

    expect(outboundCall.whenInvalidated.isCompleted).toBe(true);
  });

  it("should invalidate stage-3 compute calls on peer stop", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);
    serverHubA.acceptRpcConnection(serverConn);
    await delay(10);

    // Make a compute call
    const outboundCall = clientPeer.call(
      "CounterService.getCount:2", ["x"],
      { callTypeId: 1, outboundCallFactory: (id: number, m: string) => new RpcOutboundComputeCall(id, m) },
    ) as RpcOutboundComputeCall;

    const result = await outboundCall.result.promise;
    expect(result).toBe(0);

    // Peer stop — triggers invalidation
    clientPeer.close();
    await delay(10);

    expect(outboundCall.whenInvalidated.isCompleted).toBe(true);
  });

  it("should invalidate compute calls on host switch", async () => {
    // Connect to server A
    const [clientConnA, serverConnA] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConnA);
    clientHub.addPeer(clientPeer);
    serverHubA.acceptRpcConnection(serverConnA);
    await delay(10);

    // Make compute call on server A
    const callA = clientPeer.call(
      "CounterService.getCount:2", ["x"],
      { callTypeId: 1, outboundCallFactory: (id: number, m: string) => new RpcOutboundComputeCall(id, m) },
    ) as RpcOutboundComputeCall;
    const resultA = await callA.result.promise;
    expect(resultA).toBe(0);

    // Disconnect from A — stage-3 compute call survives
    clientConnA.close();
    await delay(10);
    expect(callA.whenInvalidated.isCompleted).toBe(false);

    // Create server B with different state
    const storeB = new Map<string, MutableState<number>>();
    const serverHubB = createServerHub(storeB);

    // Set value on B
    const sB = new MutableState(999);
    storeB.set("x", sB);

    // Connect to server B — invalidates stage-3 compute calls from A
    const [clientConnB, serverConnB] = createMessageChannelPair();
    clientPeer.connectWith(clientConnB);
    serverHubB.acceptRpcConnection(serverConnB);
    await delay(10);

    expect(callA.whenInvalidated.isCompleted).toBe(true);

    // New compute call should get B's data
    const callB = clientPeer.call(
      "CounterService.getCount:2", ["x"],
      { callTypeId: 1, outboundCallFactory: (id: number, m: string) => new RpcOutboundComputeCall(id, m) },
    ) as RpcOutboundComputeCall;
    const resultB = await callB.result.promise;
    expect(resultB).toBe(999);

    serverHubB.close();
  });

  it("should re-call after reconnect and get fresh data", async () => {
    // Connect
    const [clientConn1, serverConn1] = createMessageChannelPair();
    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn1);
    clientHub.addPeer(clientPeer);
    serverHubA.acceptRpcConnection(serverConn1);
    await delay(10);

    const counterDef = defineComputeService("CounterService", {
      getCount: { args: [""] },
    });
    const counter = createRpcClient<{ getCount(key: string): Promise<number> }>(clientPeer, counterDef);

    // Initial call
    const r1 = await counter.getCount("key1");
    expect(r1).toBe(0);

    // Disconnect
    clientConn1.close();
    await delay(10);

    // Mutate server-side state while disconnected
    let s = storeA.get("key1");
    if (s === undefined) {
      s = new MutableState(0);
      storeA.set("key1", s);
    }
    s.set(42);

    // Reconnect
    const [clientConn2, serverConn2] = createMessageChannelPair();
    clientPeer.connectWith(clientConn2);
    serverHubA.acceptRpcConnection(serverConn2);
    await delay(10);

    // Re-call — should get updated value
    const r2 = await counter.getCount("key1");
    expect(r2).toBe(42);
  });

  it("should handle server-side mutation after reconnect", async () => {
    // Connect
    const [clientConn1, serverConn1] = createMessageChannelPair();
    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn1);
    clientHub.addPeer(clientPeer);
    serverHubA.acceptRpcConnection(serverConn1);
    await delay(10);

    const counterDef = defineComputeService("CounterService", {
      getCount: { args: [""] },
    });
    const counter = createRpcClient<{ getCount(key: string): Promise<number> }>(clientPeer, counterDef);

    // Initial value
    const r1 = await counter.getCount("y");
    expect(r1).toBe(0);

    // Disconnect and reconnect
    clientConn1.close();
    await delay(10);

    const [clientConn2, serverConn2] = createMessageChannelPair();
    clientPeer.connectWith(clientConn2);
    serverHubA.acceptRpcConnection(serverConn2);
    await delay(10);

    // Mutate via RPC after reconnect
    clientPeer.callNoWait("MutationService.setCount:2", ["y", 77]);
    await delay(30);

    // Re-fetch should see the new value
    const r2 = await counter.getCount("y");
    expect(r2).toBe(77);
  });

  it("should handle compute calls with invalidation after reconnect", async () => {
    // Connect
    const [clientConn1, serverConn1] = createMessageChannelPair();
    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn1);
    clientHub.addPeer(clientPeer);
    serverHubA.acceptRpcConnection(serverConn1);
    await delay(10);

    // Make a compute call
    const call1 = clientPeer.call(
      "CounterService.getCount:2", ["z"],
      { callTypeId: 1, outboundCallFactory: (id: number, m: string) => new RpcOutboundComputeCall(id, m) },
    ) as RpcOutboundComputeCall;
    const v1 = await call1.result.promise;
    expect(v1).toBe(0);

    // Disconnect — stage-3 compute call survives
    clientConn1.close();
    await delay(10);
    expect(call1.whenInvalidated.isCompleted).toBe(false);

    // Reconnect — invalidates call1
    const [clientConn2, serverConn2] = createMessageChannelPair();
    clientPeer.connectWith(clientConn2);
    serverHubA.acceptRpcConnection(serverConn2);
    await delay(10);
    expect(call1.whenInvalidated.isCompleted).toBe(true);

    // New compute call works
    const call2 = clientPeer.call(
      "CounterService.getCount:2", ["z"],
      { callTypeId: 1, outboundCallFactory: (id: number, m: string) => new RpcOutboundComputeCall(id, m) },
    ) as RpcOutboundComputeCall;
    const v2 = await call2.result.promise;
    expect(v2).toBe(0);

    // Server-side mutation triggers invalidation on new call
    clientPeer.callNoWait("MutationService.setCount:2", ["z", 55]);
    await delay(50);
    expect(call2.whenInvalidated.isCompleted).toBe(true);
  });

  it("should NOT invalidate captured compute on disconnect alone", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);
    serverHubA.acceptRpcConnection(serverConn);
    await delay(10);

    const counterDef = defineComputeService("CounterService", {
      getCount: { args: [""] },
    });
    const counter = clientHub.addClient<{ getCount(key: string): Promise<number> }>(clientPeer, counterDef);

    const captured = await Computed.capture(() => counter.getCount("x"));
    expect(captured.value).toBe(0);
    expect(captured.isConsistent).toBe(true);

    // Disconnect — captured computed stays consistent
    clientConn.close();
    await delay(10);
    expect(captured.isConsistent).toBe(true);
  });

  it("should invalidate captured compute on reconnect", async () => {
    const [clientConn1, serverConn1] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn1);
    clientHub.addPeer(clientPeer);
    serverHubA.acceptRpcConnection(serverConn1);
    await delay(10);

    const counterDef = defineComputeService("CounterService", {
      getCount: { args: [""] },
    });
    const counter = clientHub.addClient<{ getCount(key: string): Promise<number> }>(clientPeer, counterDef);

    const captured = await Computed.capture(() => counter.getCount("x"));
    expect(captured.value).toBe(0);
    expect(captured.isConsistent).toBe(true);

    // Disconnect — captured computed stays consistent
    clientConn1.close();
    await delay(10);
    expect(captured.isConsistent).toBe(true);

    // Reconnect — triggers invalidation
    const [clientConn2, serverConn2] = createMessageChannelPair();
    clientPeer.connectWith(clientConn2);
    serverHubA.acceptRpcConnection(serverConn2);
    await delay(10);

    expect(captured.isConsistent).toBe(false);
  });

  it("should invalidate captured compute on peer stop", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);
    serverHubA.acceptRpcConnection(serverConn);
    await delay(10);

    const counterDef = defineComputeService("CounterService", {
      getCount: { args: [""] },
    });
    const counter = clientHub.addClient<{ getCount(key: string): Promise<number> }>(clientPeer, counterDef);

    const captured = await Computed.capture(() => counter.getCount("x"));
    expect(captured.value).toBe(0);
    expect(captured.isConsistent).toBe(true);

    // Peer stop — triggers invalidation
    clientPeer.close();
    await delay(10);

    expect(captured.isConsistent).toBe(false);
  });
});
