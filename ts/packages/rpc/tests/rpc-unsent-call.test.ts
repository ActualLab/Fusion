// Reconnect processing must apply only to calls actually sent on the connection
// that just died. `AllowReconnect`/`AllowResend` say what happens to a call that
// was *in flight* when the link broke; one still waiting for a connection has
// nothing to reconcile, and the reconnect that sweeps it is the one that sent it.
// `rpc-first-handshake.test.ts` covers the same defect on the first handshake.
import { describe, it, expect, afterEach } from 'vitest';
import { RetryDelaySeq } from '@actuallab/core';
import {
    RpcHub,
    RpcClientPeer,
    RpcMessageChannelConnection,
    RpcRemoteExecutionMode,
    defineRpcService,
    type WebSocketLike,
} from '../src/index.js';
import { delay } from './rpc-test-helpers.js';

const ControlServiceDef = defineRpcService('ControlService', {
    // AwaitForConnection only — mirrors ActualChat's ILiveVideoStreams.RequestKeyFrame.
    ping: {
        args: [''],
        remoteExecutionMode: RpcRemoteExecutionMode.AwaitForConnection,
    },
    pingSlowly: {
        args: [''],
        remoteExecutionMode: RpcRemoteExecutionMode.AwaitForConnection,
    },
    // AwaitForConnection | AllowReconnect — survives a same-peer reconnect,
    // but must not be resent by one it was never sent on.
    reconnectPing: {
        args: [''],
        remoteExecutionMode:
            RpcRemoteExecutionMode.AwaitForConnection | RpcRemoteExecutionMode.AllowReconnect,
    },
    reconnectPingSlowly: {
        args: [''],
        remoteExecutionMode:
            RpcRemoteExecutionMode.AwaitForConnection | RpcRemoteExecutionMode.AllowReconnect,
    },
});

interface IControlService {
    ping(id: string): Promise<string>;
    pingSlowly(id: string): Promise<string>;
    reconnectPing(id: string): Promise<string>;
    reconnectPingSlowly(id: string): Promise<string>;
}

class FakeWebSocket implements WebSocketLike {
    readyState = 0;
    onopen: ((ev: unknown) => void) | null = null;
    onmessage: ((ev: { data: unknown }) => void) | null = null;
    onclose: ((ev: { code: number; reason: string }) => void) | null = null;
    onerror: ((ev: unknown) => void) | null = null;

    private readonly _port: MessagePort;
    private readonly _sentFrames: string[];

    constructor(port: MessagePort, sentFrames: string[]) {
        this._port = port;
        this._sentFrames = sentFrames;
        port.onmessage = (ev: MessageEvent) => {
            if (this.readyState !== 1) return;
            this.onmessage?.({ data: ev.data });
        };
        setTimeout(() => {
            if (this.readyState !== 0) return;
            this.readyState = 1;
            this.onopen?.(undefined);
        }, 0);
    }

    send(data: string): void {
        if (this.readyState !== 1) return;
        this._sentFrames.push(data);
        this._port.postMessage(data);
    }

    close(code?: number, reason?: string): void {
        if (this.readyState >= 2) return;
        this.readyState = 3;
        this._port.close();
        this.onclose?.({ code: code ?? 1000, reason: reason ?? '' });
    }
}

describe('Never-sent outbound calls', () => {
    const hubs: RpcHub[] = [];
    const peers: RpcClientPeer[] = [];
    const pendingCalls: ((result: string) => void)[] = [];

    afterEach(() => {
        for (const resolve of pendingCalls) resolve('abandoned');
        pendingCalls.length = 0;
        for (const peer of peers) peer.close();
        for (const hub of hubs) hub.close();
        peers.length = 0;
        hubs.length = 0;
    });

    interface TestServer {
        hub: RpcHub;
        peerRef: string;
        /** Ids the server actually executed, in order. */
        executed: string[];
    }

    function createServer(): TestServer {
        const hub = new RpcHub('server-hub');
        hubs.push(hub);
        const server: TestServer = { hub, peerRef: 'server://peer-1', executed: [] };
        const echo = (id: unknown) => {
            server.executed.push(id as string);
            return `pong:${id as string}`;
        };
        const neverCompleting = (id: unknown) => {
            server.executed.push(id as string);
            return new Promise<string>(resolve => pendingCalls.push(resolve));
        };
        hub.addService(ControlServiceDef, {
            ping: echo,
            pingSlowly: neverCompleting,
            reconnectPing: echo,
            reconnectPingSlowly: neverCompleting,
        });
        return server;
    }

    function createClient(server: TestServer) {
        const hub = new RpcHub('client-hub');
        hubs.push(hub);
        const peer = new RpcClientPeer(hub, 'ws://test', false);
        hub.addPeer(peer);
        peers.push(peer);
        hub.reconnectDelayer.delays = RetryDelaySeq.fixed(20);

        const sentFrames: string[] = [];
        let currentWs: FakeWebSocket | undefined;
        peer.webSocketFactory = (_url: string): WebSocketLike => {
            const channel = new MessageChannel();
            server.hub.getServerPeer(server.peerRef)
                .accept(new RpcMessageChannelConnection(channel.port2));
            currentWs = new FakeWebSocket(channel.port1, sentFrames);
            return currentWs;
        };
        return {
            peer,
            sentFrames,
            client: hub.addClient<IControlService>(peer, ControlServiceDef),
            dropConnection: (): void => {
                currentWs?.close(1001, 'Server shutdown');
                currentWs = undefined;
            },
        };
    }

    /** Connect once, then drop the link — so the next handshake is a reconnect,
     *  not the peer's very first one. */
    async function connectThenDrop(
        harness: { peer: RpcClientPeer; dropConnection: () => void }
    ): Promise<void> {
        harness.peer.start();
        await harness.peer.whenConnected();
        harness.dropConnection();
        await delay(1);
        expect(harness.peer.isConnected).toBe(false);
    }

    function countFrames(sentFrames: string[], method: string): number {
        return sentFrames.filter(f => f.includes(method)).length;
    }

    it('sends a call queued while disconnected instead of sweeping it on reconnect', async () => {
        const server = createServer();
        const harness = createClient(server);
        await connectThenDrop(harness);

        // AwaitForConnection without AllowReconnect: never sent on the dead
        // connection, so the reconnect has nothing to reconcile for it.
        const resultPromise = harness.client.ping('p1');

        await expect(resultPromise).resolves.toBe('pong:p1');
        expect(server.executed).toEqual(['p1']);
    }, 10_000);

    it('sends a call queued while disconnected even when the peer changes', async () => {
        const server = createServer();
        const harness = createClient(server);
        await connectThenDrop(harness);
        server.peerRef = 'server://peer-2';

        // Nothing to resend to the new peer — the call never reached the old one.
        const resultPromise = harness.client.ping('p1');

        await expect(resultPromise).resolves.toBe('pong:p1');
        expect(server.executed).toEqual(['p1']);
    }, 10_000);

    it('sends a queued AllowReconnect call exactly once', async () => {
        const server = createServer();
        const harness = createClient(server);
        await connectThenDrop(harness);

        // The reconnect flushes it; reconnect processing must not resend it too.
        await expect(harness.client.reconnectPing('p1')).resolves.toBe('pong:p1');

        expect(server.executed).toEqual(['p1']);
        expect(countFrames(harness.sentFrames, 'ControlService.reconnectPing')).toBe(1);
    }, 10_000);

    it('sends every call queued across repeated reconnects and leaks none', async () => {
        const server = createServer();
        const harness = createClient(server);
        harness.peer.start();
        await harness.peer.whenConnected();

        const ids = ['p0', 'p1', 'p2', 'p3', 'p4'];
        for (const id of ids) {
            harness.dropConnection();
            await delay(1);
            expect(harness.peer.isConnected).toBe(false);
            await expect(harness.client.ping(id)).resolves.toBe(`pong:${id}`);
        }

        expect(server.executed).toEqual(ids);
        expect(harness.peer.outboundCalls.size).toBe(0);
    }, 10_000);

    it('still aborts an in-flight call without AllowReconnect on reconnect', async () => {
        const server = createServer();
        const harness = createClient(server);
        harness.peer.start();
        await harness.peer.whenConnected();

        const resultPromise = harness.client.pingSlowly('p1');
        resultPromise.catch(() => { /* asserted below */ });
        await delay(10);
        expect(server.executed).toEqual(['p1']); // it did reach the wire
        harness.dropConnection();

        await expect(resultPromise).rejects.toThrow('AllowReconnect');
    }, 10_000);

    it('still aborts an in-flight call without AllowResend on a peer change', async () => {
        const server = createServer();
        const harness = createClient(server);
        harness.peer.start();
        await harness.peer.whenConnected();

        const resultPromise = harness.client.reconnectPingSlowly('p1');
        resultPromise.catch(() => { /* asserted below */ });
        await delay(10);
        expect(server.executed).toEqual(['p1']);
        server.peerRef = 'server://peer-2';
        harness.dropConnection();

        await expect(resultPromise).rejects.toThrow('AllowResend');
    }, 10_000);
});
