// `_detectPeerChange` used to return a boolean, `false` both for "same peer" and
// "very first handshake", so reconnect processing ran on a cold connect too and
// its sweep rejected the calls `_setConnectionState(Connected)` had just flushed.
// .NET keeps the three-state `RpcPeerChangeKind` — see RpcPeer.cs:421-424.
import { describe, it, expect, afterEach } from 'vitest';
import { RetryDelaySeq } from '@actuallab/core';
import {
    RpcHub,
    RpcClientPeer,
    RpcMessageChannelConnection,
    RpcPeerChangeKind,
    RpcRemoteExecutionMode,
    defineRpcService,
    type RemoteHandshake,
    type WebSocketLike,
} from '../src/index.js';
import { delay } from './rpc-test-helpers.js';

// Mirrors ActualChat's ILiveVideoStreams.RequestKeyFrame: wait for a
// connection, but never survive one dropping.
const StreamControlServiceDef = defineRpcService('StreamControlService', {
    requestKeyFrame: {
        args: [''],
        remoteExecutionMode: RpcRemoteExecutionMode.AwaitForConnection,
    },
    requestKeyFrameSlowly: {
        args: [''],
        remoteExecutionMode: RpcRemoteExecutionMode.AwaitForConnection,
    },
    pushSlowly: {
        args: [''],
        remoteExecutionMode:
            RpcRemoteExecutionMode.AwaitForConnection | RpcRemoteExecutionMode.AllowReconnect,
    },
});

interface IStreamControlService {
    requestKeyFrame(streamId: string): Promise<string>;
    requestKeyFrameSlowly(streamId: string): Promise<string>;
    pushSlowly(streamId: string): Promise<string>;
}

/** Fake WebSocket over a MessagePort, so `run()` drives a real handshake.
 *  `rewriteInbound` lets a test mangle server→client frames. */
class FakeWebSocket implements WebSocketLike {
    readyState = 0;
    onopen: ((ev: unknown) => void) | null = null;
    onmessage: ((ev: { data: unknown }) => void) | null = null;
    onclose: ((ev: { code: number; reason: string }) => void) | null = null;
    onerror: ((ev: unknown) => void) | null = null;

    private readonly _port: MessagePort;

    constructor(port: MessagePort, rewriteInbound?: (data: unknown) => unknown) {
        this._port = port;
        port.onmessage = (ev: MessageEvent) => {
            if (this.readyState !== 1) return;
            this.onmessage?.({ data: rewriteInbound ? rewriteInbound(ev.data) : ev.data });
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

// A legacy .NET server predating RemotePeerId sends a handshake without one.
// Dropping the field must not make every handshake look like the very first.
function stripRemotePeerId(data: unknown): unknown {
    return typeof data === 'string' && data.includes('$sys.Handshake')
        ? data.replace(/"RemotePeerId":"[^"]*"/, '"RemotePeerId":null')
        : data;
}

describe('First-handshake reconnect sweep', () => {
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
        /** The server peer the next connection binds to — keep it for a
         *  same-peer reconnect, replace it to simulate a peer change. */
        peerRef: string;
        /** Stream ids the server actually executed `requestKeyFrame` for. */
        keyFrames: string[];
        rewriteInbound?: (data: unknown) => unknown;
    }

    function createServer(): TestServer {
        const hub = new RpcHub('server-hub');
        hubs.push(hub);
        const server: TestServer = { hub, peerRef: 'server://peer-1', keyFrames: [] };
        const neverCompleting = () => new Promise<string>(resolve => pendingCalls.push(resolve));
        hub.addService(StreamControlServiceDef, {
            requestKeyFrame: (streamId: unknown) => {
                server.keyFrames.push(streamId as string);
                return `keyframe:${streamId as string}`;
            },
            requestKeyFrameSlowly: neverCompleting,
            pushSlowly: neverCompleting,
        });
        return server;
    }

    function createWsFactory(server: TestServer) {
        let currentWs: FakeWebSocket | undefined;
        return {
            factory: (_url: string): WebSocketLike => {
                const channel = new MessageChannel();
                server.hub.getServerPeer(server.peerRef)
                    .accept(new RpcMessageChannelConnection(channel.port2));
                currentWs = new FakeWebSocket(channel.port1, server.rewriteInbound);
                return currentWs;
            },
            dropConnection: (): void => {
                currentWs?.close(1001, 'Server shutdown');
                currentWs = undefined;
            },
        };
    }

    function createClient(server: TestServer) {
        const hub = new RpcHub('client-hub');
        hubs.push(hub);
        const peer = new RpcClientPeer(hub, 'ws://test', false);
        hub.addPeer(peer);
        peers.push(peer);
        hub.reconnectDelayer.delays = RetryDelaySeq.fixed(20);
        const { factory, dropConnection } = createWsFactory(server);
        peer.webSocketFactory = factory;
        return {
            peer,
            dropConnection,
            client: hub.addClient<IStreamControlService>(peer, StreamControlServiceDef),
        };
    }

    it('keeps a call queued before the very first handshake', async () => {
        const server = createServer();
        const { peer, client } = createClient(server);

        // Registered while disconnected — that's what AwaitForConnection is for.
        const resultPromise = client.requestKeyFrame('s1');
        peer.start();

        await expect(resultPromise).resolves.toBe('keyframe:s1');
        expect(server.keyFrames).toEqual(['s1']);
    }, 10_000);

    it('keeps a call queued before the very first handshake of a legacy (no RemotePeerId) server', async () => {
        const server = createServer();
        server.rewriteInbound = stripRemotePeerId;
        const { peer, client } = createClient(server);

        const resultPromise = client.requestKeyFrame('s1');
        peer.start();

        await expect(resultPromise).resolves.toBe('keyframe:s1');
        expect(server.keyFrames).toEqual(['s1']);
    }, 10_000);

    it('still rejects an in-flight call without AllowReconnect on a same-peer reconnect', async () => {
        const server = createServer();
        const { peer, client, dropConnection } = createClient(server);
        peer.start();
        await peer.whenConnected();

        const resultPromise = client.requestKeyFrameSlowly('s1');
        resultPromise.catch(() => { /* asserted below */ });
        await delay(10);
        dropConnection();

        await expect(resultPromise).rejects.toThrow('AllowReconnect');
    }, 10_000);

    // The trap the boolean invited: deriving first-ness from "have we ever seen
    // a RemotePeerId" would make every legacy handshake look like the first,
    // so the sweep would never run at all.
    it('still rejects an in-flight call without AllowReconnect on a legacy-server reconnect', async () => {
        const server = createServer();
        server.rewriteInbound = stripRemotePeerId;
        const { peer, client, dropConnection } = createClient(server);
        peer.start();
        await peer.whenConnected();

        const resultPromise = client.requestKeyFrameSlowly('s1');
        resultPromise.catch(() => { /* asserted below */ });
        await delay(10);
        dropConnection();

        await expect(resultPromise).rejects.toThrow('AllowReconnect');
    }, 10_000);

    it('still rejects an in-flight call without AllowResend on a peer change', async () => {
        const server = createServer();
        const { peer, client, dropConnection } = createClient(server);
        peer.start();
        await peer.whenConnected();

        const resultPromise = client.pushSlowly('s1');
        resultPromise.catch(() => { /* asserted below */ });
        await delay(10);
        server.peerRef = 'server://peer-2';
        dropConnection();

        await expect(resultPromise).rejects.toThrow('AllowResend');
    }, 10_000);

    it('classifies a handshake sequence the way .NET GetPeerChangeKind does', () => {
        const { peer } = createClient(createServer());
        const detectPeerChange = (peer as unknown as {
            _detectPeerChange: (handshake: RemoteHandshake) => RpcPeerChangeKind;
        })._detectPeerChange.bind(peer);

        expect([
            detectPeerChange({ RemotePeerId: 'a' }),
            detectPeerChange({ RemotePeerId: 'a' }),
            detectPeerChange({ RemotePeerId: 'b' }),
            detectPeerChange({}),
            detectPeerChange({}),
        ]).toEqual([
            RpcPeerChangeKind.ChangedToVeryFirst,
            RpcPeerChangeKind.Unchanged,
            RpcPeerChangeKind.Changed,
            RpcPeerChangeKind.Changed,
            RpcPeerChangeKind.Unchanged,
        ]);
    });
});
