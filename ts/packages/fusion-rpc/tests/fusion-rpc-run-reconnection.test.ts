import { describe, it, expect, afterEach } from "vitest";
import { AsyncContext, RetryDelaySeq } from "@actuallab/core";
import { MutableState, ComputedState, FixedDelayer } from "@actuallab/fusion";
import {
  RpcClientPeer,
  RpcHub,
  RpcType,
  RpcMessageChannelConnection,
  defineRpcService,
  createRpcClient,
  rpcService,
  rpcMethod,
  type WebSocketLike,
} from "@actuallab/rpc";
import { FusionHub, defineComputeService, RpcOutboundComputeCall } from "../src/index.js";
import { computeMethod } from "@actuallab/fusion";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

// --- FakeWebSocket: bridges run() to in-process MessageChannel connections ---

class FakeWebSocket implements WebSocketLike {
  readyState = 0;
  onopen: ((ev: unknown) => void) | null = null;
  onmessage: ((ev: { data: unknown }) => void) | null = null;
  onclose: ((ev: { code: number; reason: string }) => void) | null = null;
  onerror: ((ev: unknown) => void) | null = null;

  private _port: MessagePort;

  constructor(port: MessagePort) {
    this._port = port;
    port.onmessage = (ev: MessageEvent) => {
      if (this.readyState === 1) {
        this.onmessage?.({ data: ev.data });
      }
    };
    setTimeout(() => {
      if (this.readyState !== 0) return;
      this.readyState = 1;
      this.onopen?.(undefined);
    }, 0);
  }

  send(data: string): void {
    if (this.readyState === 1) this._port.postMessage(data);
  }

  close(code?: number, reason?: string): void {
    if (this.readyState >= 2) return;
    this.readyState = 3;
    this._port.close();
    this.onclose?.({ code: code ?? 1000, reason: reason ?? "" });
  }
}

// --- Contracts ---

@rpcService("CounterService")
class ICounterService {
  @computeMethod @rpcMethod()
  async getCount(key: string): Promise<number> { return undefined!; }
}

const MutationServiceDef = defineRpcService("MutationService", {
  setCount: { args: ["", 0], returns: RpcType.noWait },
});

function createServerHub(name: string, store: Map<string, MutableState<number>>): FusionHub {
  const hub = new FusionHub(name);

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
  });

  hub.addService(MutationServiceDef, {
    setCount(key: unknown, value: unknown) {
      getState(key as string).set(value as number);
    },
  });

  return hub;
}

// --- Tests ---

describe("Fusion RPC run() reconnection", () => {
  const hubs: RpcHub[] = [];
  const peers: RpcClientPeer[] = [];

  afterEach(() => {
    AsyncContext.current = undefined;
    for (const p of peers) p.close();
    for (const h of hubs) h.close();
    hubs.length = 0;
    peers.length = 0;
  });

  function createWsFactory(serverRef: { hub: FusionHub }) {
    let currentWs: FakeWebSocket | undefined;

    const factory = (_url: string): WebSocketLike => {
      const channel = new MessageChannel();
      serverRef.hub.acceptRpcConnection(new RpcMessageChannelConnection(channel.port2));
      const fakeWs = new FakeWebSocket(channel.port1);
      currentWs = fakeWs;
      return fakeWs;
    };

    return {
      factory,
      closeCurrentWs(code = 1001, reason = "Server shutdown") {
        currentWs?.close(code, reason);
        currentWs = undefined;
      },
    };
  }

  it("should call compute methods after reconnect via run()", async () => {
    const store = new Map<string, MutableState<number>>();
    const serverRef = { hub: createServerHub("server-1", store) };
    hubs.push(serverRef.hub);

    const clientHub = new FusionHub("client");
    hubs.push(clientHub);
    const peer = new RpcClientPeer(clientHub, "ws://test");
    clientHub.addPeer(peer);
    peers.push(peer);
    peer.reconnectDelayer.delays = RetryDelaySeq.fixed(50);

    const { factory, closeCurrentWs } = createWsFactory(serverRef);
    void peer.run(factory);

    await peer.connected.whenNext();
    await delay(10);

    const counterDef = defineComputeService("CounterService", {
      getCount: { args: [""] },
    });
    const counter = createRpcClient<{ getCount(key: string): Promise<number> }>(peer, counterDef);

    // Initial call
    const r1 = await counter.getCount("x");
    expect(r1).toBe(0);

    // --- Server restart ---
    closeCurrentWs();
    await delay(10);

    const store2 = new Map<string, MutableState<number>>();
    store2.set("x", new MutableState(42));
    serverRef.hub = createServerHub("server-2", store2);
    hubs.push(serverRef.hub);

    // Wait for reconnection
    await peer.connected.whenNext();
    await delay(10);

    // Call after reconnection should get new server's data
    const r2 = await counter.getCount("x");
    expect(r2).toBe(42);
  }, 10_000);

  it("should invalidate stage-3 compute calls on reconnect via run()", async () => {
    const store = new Map<string, MutableState<number>>();
    const serverRef = { hub: createServerHub("server-1", store) };
    hubs.push(serverRef.hub);

    const clientHub = new FusionHub("client");
    hubs.push(clientHub);
    const peer = new RpcClientPeer(clientHub, "ws://test");
    clientHub.addPeer(peer);
    peers.push(peer);
    peer.reconnectDelayer.delays = RetryDelaySeq.fixed(50);

    const { factory, closeCurrentWs } = createWsFactory(serverRef);
    void peer.run(factory);

    await peer.connected.whenNext();
    await delay(10);

    // Make a compute call directly (low-level, like the fusion-rpc tests)
    const outboundCall = peer.call(
      "CounterService.getCount:2", ["x"],
      { callTypeId: 1, outboundCallFactory: (id, m) => new RpcOutboundComputeCall(id, m) },
    ) as RpcOutboundComputeCall;
    const result = await outboundCall.result.promise;
    expect(result).toBe(0);

    // Stage-3: result resolved, awaiting invalidation
    expect(outboundCall.whenInvalidated.isCompleted).toBe(false);

    // --- Server restart ---
    closeCurrentWs();
    await delay(10);

    const store2 = new Map<string, MutableState<number>>();
    serverRef.hub = createServerHub("server-2", store2);
    hubs.push(serverRef.hub);

    // Wait for reconnection — invalidateAll() fires in run() loop
    await peer.connected.whenNext();
    await delay(10);

    // Stage-3 compute call should be invalidated
    expect(outboundCall.whenInvalidated.isCompleted).toBe(true);
  }, 10_000);

  it("should receive server-side invalidation after reconnect via run()", async () => {
    const store = new Map<string, MutableState<number>>();
    const serverRef = { hub: createServerHub("server-1", store) };
    hubs.push(serverRef.hub);

    const clientHub = new FusionHub("client");
    hubs.push(clientHub);
    const peer = new RpcClientPeer(clientHub, "ws://test");
    clientHub.addPeer(peer);
    peers.push(peer);
    peer.reconnectDelayer.delays = RetryDelaySeq.fixed(50);

    const { factory, closeCurrentWs } = createWsFactory(serverRef);
    void peer.run(factory);

    await peer.connected.whenNext();
    await delay(10);

    // Make a compute call
    const outboundCall = peer.call(
      "CounterService.getCount:2", ["x"],
      { callTypeId: 1, outboundCallFactory: (id, m) => new RpcOutboundComputeCall(id, m) },
    ) as RpcOutboundComputeCall;
    const v1 = await outboundCall.result.promise;
    expect(v1).toBe(0);

    // --- Server restart (same store, same server hub) ---
    closeCurrentWs();
    await delay(10);

    // Re-use same store — no restart, just reconnect
    serverRef.hub = createServerHub("server-1-revived", store);
    hubs.push(serverRef.hub);

    await peer.connected.whenNext();
    await delay(10);

    // Old compute call should be invalidated by reconnection
    expect(outboundCall.whenInvalidated.isCompleted).toBe(true);

    // New compute call should work and receive server-side invalidation
    const call2 = peer.call(
      "CounterService.getCount:2", ["x"],
      { callTypeId: 1, outboundCallFactory: (id, m) => new RpcOutboundComputeCall(id, m) },
    ) as RpcOutboundComputeCall;
    const v2 = await call2.result.promise;
    expect(v2).toBe(0);
    expect(call2.whenInvalidated.isCompleted).toBe(false);

    // Mutate server-side state → should trigger invalidation
    peer.callNoWait("MutationService.setCount:3", ["x", 99]);
    await delay(50);
    expect(call2.whenInvalidated.isCompleted).toBe(true);
  }, 10_000);
});
