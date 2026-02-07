import { describe, it, expect, beforeEach, afterEach } from "vitest";
import {
  FusionHub,
  MutableState,
  ComputeContext,
  computedRegistry,
} from "../src/index.js";
import {
  RpcHub,
  RpcClientPeer,
  RpcOutboundComputeCall,
  defineComputeService,
  defineRpcService,
  createRpcClient,
} from "@actuallab/rpc";
import { createMockWsPair } from "../../rpc/tests/mock-ws.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

const CounterServiceDef = defineComputeService("CounterService", {
  getCount: { args: [""] },
  getDoubled: { args: [""] },
});

const MutationServiceDef = defineRpcService("MutationService", {
  setCount: { args: ["", 0] },
});

interface ICounterService {
  getCount(key: string): Promise<number>;
  getDoubled(key: string): Promise<number>;
}

interface IMutationService {
  setCount(key: string, value: number): Promise<void>;
}

describe("End-to-end Fusion over RPC", () => {
  let serverHub: FusionHub;
  let clientHub: RpcHub;
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
    computedRegistry.clear();
    ComputeContext.current = undefined;
    store.clear();

    serverHub = new FusionHub("server");
    clientHub = new RpcHub("client");

    // Register compute service on server
    serverHub.registerComputeService(CounterServiceDef, {
      getCount(key: unknown) {
        return getState(key as string).value;
      },
      getDoubled(key: unknown) {
        return getState(key as string).value * 2;
      },
    });

    // Register mutation service (non-compute)
    serverHub.registerPlainService(MutationServiceDef, {
      setCount(key: unknown, value: unknown) {
        getState(key as string).set(value as number);
      },
    });
  });

  afterEach(() => {
    serverHub.close();
    clientHub.close();
  });

  it("should call a compute method and get cached result", async () => {
    const [clientWs, serverWs] = createMockWsPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientWs);
    clientHub.addPeer(clientPeer);

    serverHub.acceptConnection(serverWs);

    await delay(10);

    const counter = createRpcClient<ICounterService>(clientPeer, CounterServiceDef);
    getState("x").set(42);

    const result = await counter.getCount("x");
    expect(result).toBe(42);
  });

  it("should receive $sys-c.Invalidate when server-side computed is invalidated", async () => {
    const [clientWs, serverWs] = createMockWsPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientWs);
    clientHub.addPeer(clientPeer);

    serverHub.acceptConnection(serverWs);

    await delay(10);

    // Make a compute call
    const outboundCall = clientPeer.call(
      "CounterService.getCount",
      ["x"],
      true, // compute = true
    ) as RpcOutboundComputeCall;

    const result = await outboundCall.result.promise;
    expect(result).toBe(0); // default value

    // Trigger server-side invalidation via mutation
    const mutator = createRpcClient<IMutationService>(clientPeer, MutationServiceDef);
    await mutator.setCount("x", 100);

    // Wait for invalidation notification
    await delay(50);
    expect(outboundCall.whenInvalidated.isCompleted).toBe(true);
  });

  it("should call multiple compute methods", async () => {
    const [clientWs, serverWs] = createMockWsPair();

    const clientPeer = new RpcClientPeer("client", clientHub, "ws://test");
    clientPeer.connectWith(clientWs);
    clientHub.addPeer(clientPeer);

    serverHub.acceptConnection(serverWs);

    await delay(10);

    const counter = createRpcClient<ICounterService>(clientPeer, CounterServiceDef);
    getState("a").set(10);

    const count = await counter.getCount("a");
    expect(count).toBe(10);

    const doubled = await counter.getDoubled("a");
    expect(doubled).toBe(20);
  });
});
