import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { AsyncContext } from "@actuallab/core";
import {
  Computed,
  MutableState,
  computeMethod,
} from "@actuallab/fusion";
import {
  RpcClientPeer,
  RpcType,
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

// Legacy service def for mutation (non-compute)
const MutationServiceDef = defineRpcService("MutationService", {
  setCount: { args: ["", 0], returns: RpcType.noWait },
});

interface IMutationService {
  setCount(key: string, value: number): void;
}

describe("End-to-end Fusion over RPC", () => {
  let serverHub: FusionHub;
  let clientHub: FusionHub;
  const store = new Map<string, MutableState<number>>();

  function getState(key: string): MutableState<number> {
    let s = store.get(key);
    if (s === undefined) {
      s = new MutableState(0);
      store.set(key, s);
    }
    return s;
  }

  beforeEach(() => {
    AsyncContext.current = undefined;
    store.clear();

    serverHub = new FusionHub("server");
    clientHub = new FusionHub("client");

    // Register compute service on server using contract class
    serverHub.addService(ICounterService, {
      async getCount(key: unknown): Promise<number> {
        return getState(key as string).use();
      },
      async getDoubled(key: unknown): Promise<number> {
        return getState(key as string).use() * 2;
      },
    });

    // Register mutation service (non-compute, noWait)
    serverHub.addService(MutationServiceDef, {
      setCount(key: unknown, value: unknown) {
        getState(key as string).set(value as number);
      },
    });
  });

  afterEach(() => {
    serverHub.close();
    clientHub.close();
  });

  it("should call a compute method and get result", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer(clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    serverHub.acceptRpcConnection(serverConn);

    await delay(10);

    // Use legacy service def for client (decorator-based client coming in future)
    const counterDef = defineComputeService("CounterService", {
      getCount: { args: [""] },
      getDoubled: { args: [""] },
    });

    const counter = createRpcClient<{ getCount(key: string): Promise<number>; getDoubled(key: string): Promise<number> }>(clientPeer, counterDef);
    getState("x").set(42);

    const result = await counter.getCount("x");
    expect(result).toBe(42);
  });

  it("should receive $sys-c.Invalidate when server-side computed is invalidated", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer(clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    serverHub.acceptRpcConnection(serverConn);

    await delay(10);

    // Make a compute call
    const outboundCall = clientPeer.call(
      "CounterService.getCount:2",
      ["x"],
      { callTypeId: 1, outboundCallFactory: (id, m) => new RpcOutboundComputeCall(id, m) },
    ) as RpcOutboundComputeCall;

    const result = await outboundCall.result.promise;
    expect(result).toBe(0); // default value

    // Trigger server-side invalidation via mutation (noWait)
    clientPeer.callNoWait("MutationService.setCount:3", ["x", 100]);

    // Wait for invalidation notification
    await delay(50);
    expect(outboundCall.whenInvalidated.isCompleted).toBe(true);
  });

  it("should call multiple compute methods", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer(clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    serverHub.acceptRpcConnection(serverConn);

    await delay(10);

    const counterDef = defineComputeService("CounterService", {
      getCount: { args: [""] },
      getDoubled: { args: [""] },
    });

    const counter = createRpcClient<{ getCount(key: string): Promise<number>; getDoubled(key: string): Promise<number> }>(clientPeer, counterDef);
    getState("a").set(10);

    const count = await counter.getCount("a");
    expect(count).toBe(10);

    const doubled = await counter.getDoubled("a");
    expect(doubled).toBe(20);
  });

  it("should capture RPC compute call via Computed.capture()", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer(clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    serverHub.acceptRpcConnection(serverConn);

    await delay(10);

    const counterDef = defineComputeService("CounterService", {
      getCount: { args: [""] },
    });
    const counter = clientHub.addClient<{ getCount(key: string): Promise<number> }>(clientPeer, counterDef);
    getState("x").set(42);

    const captured = await Computed.capture(() => counter.getCount("x"));
    expect(captured.value).toBe(42);
    expect(captured.isConsistent).toBe(true);
  });

  it("should observe server-side invalidation via Computed.capture()", async () => {
    const [clientConn, serverConn] = createMessageChannelPair();

    const clientPeer = new RpcClientPeer(clientHub, "ws://test");
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    serverHub.acceptRpcConnection(serverConn);

    await delay(10);

    const counterDef = defineComputeService("CounterService", {
      getCount: { args: [""] },
    });
    const counter = clientHub.addClient<{ getCount(key: string): Promise<number> }>(clientPeer, counterDef);

    const captured = await Computed.capture(() => counter.getCount("x"));
    expect(captured.value).toBe(0);

    // Trigger server-side invalidation via mutation
    clientPeer.callNoWait("MutationService.setCount:3", ["x", 100]);

    // Wait for invalidation notification
    await captured.whenInvalidated();
    expect(captured.isConsistent).toBe(false);
  });
});
