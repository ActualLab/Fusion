import { describe, it, expect, afterEach } from 'vitest';
import { RetryDelaySeq } from '@actuallab/core';
import {
    RpcHub,
    RpcClientPeer,
    RpcMessageChannelConnection,
    defineRpcService,
    createRpcClient,
    type WebSocketLike,
} from '../src/index.js';

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
 * WebSocket that never reaches the OPEN state and never fires onclose / onerror —
 * simulates a hung connect (e.g. mobile after device sleep, or a half-open TCP
 * connection the OS hasn't noticed is dead). Tracks close() calls so we can
 * assert the timeout fix tore it down rather than leaking it.
 */
class HangingWebSocket implements WebSocketLike {
    readyState = 0; // CONNECTING — stays here until close()
    onopen: ((ev: unknown) => void) | null = null;
    onmessage: ((ev: { data: unknown }) => void) | null = null;
    onclose: ((ev: { code: number; reason: string }) => void) | null = null;
    onerror: ((ev: unknown) => void) | null = null;
    closeCount = 0;

    send(): void {
        // Won't be called — we never open.
    }

    close(code?: number, reason?: string): void {
        if (this.readyState >= 2) return;
        this.closeCount++;
        this.readyState = 3;
        // Fire onclose asynchronously so the run loop's `closedRejection`
        // observer sees it (synchronous fire would race the awaiter).
        queueMicrotask(() => {
            this.onclose?.({ code: code ?? 1006, reason: reason ?? '' });
        });
    }
}

/**
 * Factory that returns hanging WebSockets the first `failCount` times, then
 * a working FakeWebSocket bridged to a server hub via MessagePort.
 */
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
            if (this.readyState === 1) this.onmessage?.({ data: ev.data });
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
        this.onclose?.({ code: code ?? 1000, reason: reason ?? '' });
    }
}

describe('RpcClientPeer connect timeout', () => {
    const hubs: RpcHub[] = [];
    const peers: RpcClientPeer[] = [];

    afterEach(() => {
        for (const p of peers) p.close();
        for (const h of hubs) h.close();
        hubs.length = 0;
        peers.length = 0;
    });

    it('fires connectTimeoutMs when WS never opens, then recovers on next attempt', async () => {
        // --- Server (unused until the third attempt) ---
        const serverHub = new RpcHub('server');
        serverHub.addService(CalcServiceDef, {
            add: (a: unknown, b: unknown) => (a as number) + (b as number),
        });
        hubs.push(serverHub);

        const clientHub = new RpcHub('client');
        hubs.push(clientHub);
        const peer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientHub.addPeer(peer);
        peers.push(peer);

        // Tight timing for the test
        peer.connectTimeoutMs = 100;
        peer.hub.reconnectDelayer.delays = RetryDelaySeq.fixed(20);

        const hangingSockets: HangingWebSocket[] = [];
        let attempt = 0;
        peer.webSocketFactory = (_url: string): WebSocketLike => {
            attempt++;
            // First two attempts hang; third one connects to the real server.
            if (attempt <= 2) {
                const ws = new HangingWebSocket();
                hangingSockets.push(ws);
                return ws;
            }
            const channel = new MessageChannel();
            const ref = `server://${crypto.randomUUID()}`;
            serverHub
                .getServerPeer(ref)
                .accept(new RpcMessageChannelConnection(channel.port2));
            return new FakeWebSocket(channel.port1);
        };

        peer.start();

        // Wait for a successful Connected — the loop must have timed out twice,
        // then succeeded. Bound generously vs. the 100 ms timeout x 2 + retry delays.
        await Promise.race([
            peer.whenConnected(),
            delay(2_000).then(() => {
                throw new Error('Peer never reached Connected within budget');
            }),
        ]);

        // Both hanging sockets must have been close()'d by the timeout — otherwise
        // we'd be leaking sockets exactly like the WASM scenario from the bug report.
        expect(hangingSockets).toHaveLength(2);
        for (const ws of hangingSockets) {
            expect(ws.closeCount).toBeGreaterThan(0);
            expect(ws.readyState).toBe(3); // CLOSED
        }

        // Sanity: real call works after recovery.
        const calc = createRpcClient<ICalcService>(peer, CalcServiceDef);
        const r = await calc.add(2, 3);
        expect(r).toBe(5);
    }, 5_000);

    it('does not fire when the WS opens within connectTimeoutMs', async () => {
        // Sanity: with a generous timeout and a normal (fast) connect, nothing fires.
        const serverHub = new RpcHub('server');
        serverHub.addService(CalcServiceDef, {
            add: (a: unknown, b: unknown) => (a as number) + (b as number),
        });
        hubs.push(serverHub);

        const clientHub = new RpcHub('client');
        hubs.push(clientHub);
        const peer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientHub.addPeer(peer);
        peers.push(peer);

        peer.connectTimeoutMs = 5_000;
        peer.hub.reconnectDelayer.delays = RetryDelaySeq.fixed(20);

        let factoryCount = 0;
        peer.webSocketFactory = (_url: string): WebSocketLike => {
            factoryCount++;
            const channel = new MessageChannel();
            const ref = `server://${crypto.randomUUID()}`;
            serverHub
                .getServerPeer(ref)
                .accept(new RpcMessageChannelConnection(channel.port2));
            return new FakeWebSocket(channel.port1);
        };

        peer.start();
        await peer.whenConnected();

        // Exactly one WS was created — no retry was triggered by a spurious timeout.
        expect(factoryCount).toBe(1);

        const calc = createRpcClient<ICalcService>(peer, CalcServiceDef);
        expect(await calc.add(1, 1)).toBe(2);
    }, 5_000);
});
