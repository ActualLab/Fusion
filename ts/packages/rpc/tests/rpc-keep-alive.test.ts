import { describe, it, expect, afterEach } from 'vitest';
import { RetryDelaySeq } from '@actuallab/core';
import {
    RpcHub,
    RpcClientPeer,
    RpcConnectionState,
    RpcLimits,
    RpcMessageChannelConnection,
    defineRpcService,
    createRpcClient,
    type WebSocketLike,
} from '../src/index.js';

const KEEP_ALIVE_PERIOD_MS = 2_000;
const KEEP_ALIVE_TIMEOUT_MS = 5_000;

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

interface ICalcService {
    add(a: number, b: number): Promise<number>;
}

const CalcServiceDef = defineRpcService('CalcService', {
    add: { args: [0, 0] },
});

/**
 * Fake WebSocket backed by a MessagePort whose inbound (server -> client)
 * delivery can be severed via `inboundOpen` while the socket stays open —
 * used to simulate a half-open link that only the keep-alive watchdog can
 * detect.
 */
class GateableFakeWebSocket implements WebSocketLike {
    readyState = 0; // CONNECTING
    onopen: ((ev: unknown) => void) | null = null;
    onmessage: ((ev: { data: unknown }) => void) | null = null;
    onclose: ((ev: { code: number; reason: string }) => void) | null = null;
    onerror: ((ev: unknown) => void) | null = null;
    inboundOpen = true;
    private _port: MessagePort;

    constructor(port: MessagePort) {
        this._port = port;
        port.onmessage = (ev: MessageEvent): void => {
            if (this.readyState === 1 && this.inboundOpen)
                this.onmessage?.({ data: ev.data });
        };
        setTimeout(() => {
            if (this.readyState !== 0) return; // already closed

            this.readyState = 1;
            this.onopen?.(undefined);
        }, 0);
    }

    send(data: string): void {
        if (this.readyState === 1)
            this._port.postMessage(data);
    }

    close(code?: number, reason?: string): void {
        if (this.readyState >= 2) return;

        this.readyState = 3;
        this._port.close();
        this.onclose?.({ code: code ?? 1000, reason: reason ?? '' });
    }
}

describe('RPC KeepAlive', () => {
    const hubs: RpcHub[] = [];
    const peers: RpcClientPeer[] = [];

    afterEach(() => {
        for (const p of peers) p.close();
        for (const h of hubs) h.close();
        hubs.length = 0;
        peers.length = 0;
    });

    it('keeps the connection alive while $sys.KeepAlive flows', async () => {
        const { peer } = setup();

        let keepAliveCount = 0;
        const notifyKeepAlive = peer.notifyKeepAliveReceived.bind(peer);
        peer.notifyKeepAliveReceived = (): void => {
            keepAliveCount++;
            notifyKeepAlive();
        };

        let disconnectCount = 0;
        peer.connectionStateChanged.add(state => {
            if (state === RpcConnectionState.Disconnected)
                disconnectCount++;
        });

        peer.start();
        await peer.whenConnected();

        const calc = createRpcClient<ICalcService>(peer, CalcServiceDef);
        expect(await calc.add(2, 3)).toBe(5);

        // Idle past the timeout: without inbound keep-alives the watchdog would
        // have force-closed the socket at keepAliveTimeoutMs.
        await delay(KEEP_ALIVE_TIMEOUT_MS + KEEP_ALIVE_PERIOD_MS);

        expect(peer.isConnected).toBe(true);
        expect(peer.connection).toBeDefined();
        expect(disconnectCount).toBe(0);
        // Keep-alives actually arrived (~one per period).
        expect(keepAliveCount).toBeGreaterThanOrEqual(2);
        expect(await calc.add(10, 20)).toBe(30);
    }, 20_000);

    it('drops the connection when $sys.KeepAlive stops, then reconnects', async () => {
        const { peer, wsFactory } = setup();

        peer.start();
        await peer.whenConnected();

        const calc = createRpcClient<ICalcService>(peer, CalcServiceDef);
        expect(await calc.add(1, 1)).toBe(2);

        // Sever inbound delivery: the server keeps sending $sys.KeepAlive but the
        // client never sees them while the socket stays open. The watchdog must
        // force-close it.
        const severedWs = wsFactory.current;
        expect(severedWs).toBeDefined();
        severedWs!.inboundOpen = false;

        const severedAt = Date.now();
        await waitForDisconnect(peer, KEEP_ALIVE_TIMEOUT_MS + KEEP_ALIVE_PERIOD_MS + 3_000);
        const elapsed = Date.now() - severedAt;

        // The watchdog can't fire before the timeout window elapses. The last
        // keep-alive may have arrived up to one period before we severed delivery.
        expect(elapsed).toBeGreaterThanOrEqual(KEEP_ALIVE_TIMEOUT_MS - KEEP_ALIVE_PERIOD_MS);

        // The reconnect loop builds a fresh (ungated) socket and recovers.
        await peer.whenConnected();
        expect(await calc.add(3, 4)).toBe(7);
    }, 25_000);

    // Private methods

    function setup(): {
        serverHub: RpcHub;
        clientHub: RpcHub;
        peer: RpcClientPeer;
        wsFactory: ReturnType<typeof createWsFactory>;
        } {
        const limits = new RpcLimits({
            keepAlivePeriodMs: KEEP_ALIVE_PERIOD_MS,
            keepAliveTimeoutMs: KEEP_ALIVE_TIMEOUT_MS,
        });

        const serverHub = new RpcHub('server-hub');
        serverHub.limits = limits;
        serverHub.addService(CalcServiceDef, {
            add: (a: unknown, b: unknown) => (a as number) + (b as number),
        });
        hubs.push(serverHub);

        const clientHub = new RpcHub('client-hub');
        clientHub.limits = limits;
        const peer = new RpcClientPeer(clientHub, 'ws://test', false);
        peer.hub.reconnectDelayer.delays = RetryDelaySeq.fixed(50);
        clientHub.addPeer(peer);
        hubs.push(clientHub);
        peers.push(peer);

        const wsFactory = createWsFactory(serverHub);
        peer.webSocketFactory = wsFactory.factory;
        return { serverHub, clientHub, peer, wsFactory };
    }

    function createWsFactory(serverHub: RpcHub): {
        factory: (url: string) => WebSocketLike;
        readonly current: GateableFakeWebSocket | undefined;
        } {
        let currentWs: GateableFakeWebSocket | undefined;
        return {
            factory: (_url: string): WebSocketLike => {
                const channel = new MessageChannel();
                const ref = `server://${crypto.randomUUID()}`;
                serverHub.getServerPeer(ref).accept(new RpcMessageChannelConnection(channel.port2));
                currentWs = new GateableFakeWebSocket(channel.port1);
                return currentWs;
            },
            get current(): GateableFakeWebSocket | undefined {
                return currentWs;
            },
        };
    }

    function waitForDisconnect(peer: RpcClientPeer, timeoutMs: number): Promise<void> {
        return new Promise<void>((resolve, reject) => {
            const handler = (state: RpcConnectionState): void => {
                if (state !== RpcConnectionState.Disconnected) return;

                clearTimeout(timer);
                peer.connectionStateChanged.remove(handler);
                resolve();
            };
            const timer = setTimeout(() => {
                peer.connectionStateChanged.remove(handler);
                reject(new Error(`No disconnect within ${timeoutMs}ms`));
            }, timeoutMs);
            peer.connectionStateChanged.add(handler);
        });
    }
});
