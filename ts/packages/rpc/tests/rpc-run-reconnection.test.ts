import { describe, it, expect, afterEach } from "vitest";
import { RetryDelaySeq } from "@actuallab/core";
import {
  RpcHub,
  RpcClientPeer,
  RpcMessageChannelConnection,
  defineRpcService,
  createRpcClient,
  type WebSocketLike,
} from "../src/index.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

interface ICalcService {
  add(a: number, b: number): Promise<number>;
}

const CalcServiceDef = defineRpcService("CalcService", {
  add: { args: [0, 0] },
});

/**
 * Fake WebSocket backed by a MessagePort — allows run() to work
 * with in-process test connections instead of real network sockets.
 */
class FakeWebSocket implements WebSocketLike {
  readyState = 0; // CONNECTING
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
    // Simulate async WebSocket open
    setTimeout(() => {
      if (this.readyState !== 0) return; // already closed
      this.readyState = 1;
      this.onopen?.(undefined);
    }, 0);
  }

  send(data: string): void {
    if (this.readyState === 1) {
      this._port.postMessage(data);
    }
  }

  close(code?: number, reason?: string): void {
    if (this.readyState >= 2) return;
    this.readyState = 3;
    this._port.close();
    this.onclose?.({ code: code ?? 1000, reason: reason ?? "" });
  }
}

describe("RPC run() reconnection", () => {
  const hubs: RpcHub[] = [];
  const peers: RpcClientPeer[] = [];

  afterEach(() => {
    for (const p of peers) p.close();
    for (const h of hubs) h.close();
    hubs.length = 0;
    peers.length = 0;
  });

  function createServerHub(name: string, addResult = 0): RpcHub {
    const hub = new RpcHub(name);
    hub.addService(CalcServiceDef, {
      add: (a: unknown, b: unknown) => (a as number) + (b as number) + addResult,
    });
    hubs.push(hub);
    return hub;
  }

  /**
   * Creates a wsFactory for run() that connects to whichever server hub
   * is referenced by `serverRef.hub` at the time the WS opens.
   * Returns the factory and a handle to close the current fake WS.
   */
  function createWsFactory(serverRef: { hub: RpcHub }) {
    let currentWs: FakeWebSocket | undefined;

    const factory = (_url: string): WebSocketLike => {
      const channel = new MessageChannel();

      // Connect server side via getServerPeer + accept
      const ref = `server://${crypto.randomUUID()}`;
      const serverPeer = serverRef.hub.getServerPeer(ref);
      serverPeer.accept(new RpcMessageChannelConnection(channel.port2));

      const fakeWs = new FakeWebSocket(channel.port1);
      currentWs = fakeWs;
      return fakeWs;
    };

    return {
      factory,
      /** Simulate server dropping the connection. */
      closeCurrentWs(code = 1001, reason = "Server shutdown") {
        currentWs?.close(code, reason);
        currentWs = undefined;
      },
    };
  }

  it("should reconnect via run() and call after server restart", async () => {
    // --- Server 1 ---
    const serverRef = { hub: createServerHub("server-1") };

    const clientHub = new RpcHub("client");
    hubs.push(clientHub);
    const peer = new RpcClientPeer(clientHub, "ws://test");
    clientHub.addPeer(peer);
    peers.push(peer);

    // Use short delays for fast test
    peer.reconnectDelayer.delays = RetryDelaySeq.fixed(50);

    const { factory, closeCurrentWs } = createWsFactory(serverRef);

    // Start the reconnection loop
    void peer.run(factory);

    // Wait for initial connection
    await peer.connected.whenNext();
    await delay(10);

    // Verify calls work
    const calc = createRpcClient<ICalcService>(peer, CalcServiceDef);
    const r1 = await calc.add(1, 2);
    expect(r1).toBe(3);

    // --- Simulate server restart ---
    closeCurrentWs();
    await delay(10);

    // Create server 2 (different hubId = server restart)
    serverRef.hub = createServerHub("server-2", 100);

    // Wait for reconnection
    await peer.connected.whenNext();
    await delay(10);

    // Verify calls go to the new server
    const r2 = await calc.add(1, 2);
    expect(r2).toBe(103);
  }, 10_000);

  it("should reconnect via run() multiple times", async () => {
    const serverRef = { hub: createServerHub("server-1") };

    const clientHub = new RpcHub("client");
    hubs.push(clientHub);
    const peer = new RpcClientPeer(clientHub, "ws://test");
    clientHub.addPeer(peer);
    peers.push(peer);

    peer.reconnectDelayer.delays = RetryDelaySeq.fixed(50);

    const { factory, closeCurrentWs } = createWsFactory(serverRef);
    void peer.run(factory);

    const calc = createRpcClient<ICalcService>(peer, CalcServiceDef);

    for (let i = 0; i < 3; i++) {
      await peer.connected.whenNext();
      await delay(10);

      const result = await calc.add(i, 10);
      expect(result).toBe(i + 10);

      // Server restart
      closeCurrentWs();
      await delay(10);
      serverRef.hub = createServerHub(`server-${i + 2}`);
    }

    // Final reconnection + call
    await peer.connected.whenNext();
    await delay(10);
    const final = await calc.add(99, 1);
    expect(final).toBe(100);
  }, 15_000);
});
