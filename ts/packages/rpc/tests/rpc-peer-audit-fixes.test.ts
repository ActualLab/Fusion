// Regression tests for the RPC-peer batch of docs/plans/ts-port-audit.md:
//   R5  — pre-handshake sends ($sys.Cancel, stream acks) are held/dropped
//   R8  — peer-change detection keys on RemotePeerId, not RemoteHubId
//   R9  — a resent inbound call id must not re-execute the handler;
//         completed-call retention is bounded
//   R13 — a premature disconnect keeps the reconnect backoff index growing,
//         and the default delayer turns that into growing delays
//   R16 — a stale $sys.Reconnect (wrong handshake index) is rejected
import { describe, it, expect, afterEach } from 'vitest';
import { RetryDelaySeq } from '@actuallab/core';
import {
    RpcHub,
    RpcClientPeer,
    RpcClientPeerReconnectDelayer,
    RpcInboundCall,
    RpcInboundCallTracker,
    RpcStream,
    RpcWebSocketConnection,
    RpcMessageChannelConnection,
    RpcSerializationFormat,
    RpcSystemCalls,
    defineRpcService,
    parseStreamRef,
    type RpcServerPeer,
    type WebSocketLike,
} from '../src/index.js';
import { createMockWsPair } from './mock-ws.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

async function waitFor(cond: () => boolean, timeoutMs = 2000): Promise<void> {
    const start = Date.now();
    while (!cond()) {
        if (Date.now() - start > timeoutMs)
            throw new Error('waitFor timed out');

        await delay(5);
    }
}

/** MessagePort-backed WebSocket so `run()` drives an in-process server peer. */
class FakeWebSocket implements WebSocketLike {
    readyState = 0;
    onopen: ((ev: unknown) => void) | null = null;
    onmessage: ((ev: { data: unknown }) => void) | null = null;
    onclose: ((ev: { code: number; reason: string }) => void) | null = null;
    onerror: ((ev: unknown) => void) | null = null;

    constructor(private _port: MessagePort) {
        _port.onmessage = (ev: MessageEvent) => {
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

describe('RPC peer audit fixes (R5, R8, R9, R13, R16)', () => {
    const hubs: RpcHub[] = [];
    const peers: RpcClientPeer[] = [];

    afterEach(() => {
        for (const p of peers) p.close();
        for (const h of hubs) h.close();
        hubs.length = 0;
        peers.length = 0;
    });

    it('R9: a duplicate inbound call id must not re-execute the handler', async () => {
        const serverHub = new RpcHub('server-hub');
        const clientHub = new RpcHub('client-hub');
        hubs.push(serverHub, clientHub);
        const format = RpcSerializationFormat.get('json5np');
        const [clientWs, serverWs] = createMockWsPair();

        const sentFrames: unknown[] = [];
        const origSend = clientWs.send.bind(clientWs);
        clientWs.send = data => {
            sentFrames.push(data);
            origSend(data);
        };

        const clientConn = new RpcWebSocketConnection(
            clientWs, format.isBinary, format, clientHub.registry);
        const serverConn = new RpcWebSocketConnection(
            serverWs, format.isBinary, format, serverHub.registry);
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test', false);
        peers.push(clientPeer);
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        serverHub.getServerPeer('server://test').accept(serverConn);
        await delay(10);

        let execCount = 0;
        const SvcDef = defineRpcService('DupService', { inc: { args: [''] } });
        serverHub.addService(SvcDef, {
            inc(_key: unknown) {
                execCount++;
                return execCount;
            },
        });
        const client = clientHub.addClient<{ inc(k: string): Promise<number> }>(
            clientPeer, SvcDef);

        await client.inc('a');
        expect(execCount).toBe(1);

        // Replay the exact call frame — simulates a reconnect resend of the
        // same call id, which both sides produce by design.
        const callFrame = sentFrames.find(
            f => typeof f === 'string' && f.includes('DupService.inc'));
        expect(callFrame).toBeDefined();
        origSend(callFrame as string);
        await delay(20);

        // GetOrRegister: a known call id attaches to the existing call and
        // re-sends its result — it never re-executes.
        expect(execCount).toBe(1);
    });

    it('R5: aborting a pre-handshake call does not put $sys.Cancel on the wire', async () => {
        const clientHub = new RpcHub('client-hub');
        hubs.push(clientHub);
        const format = RpcSerializationFormat.get('json5np');
        const [clientWs] = createMockWsPair();
        await delay(5); // let the mock socket reach OPEN

        const sent: string[] = [];
        const origSend = clientWs.send.bind(clientWs);
        clientWs.send = data => {
            if (typeof data === 'string') sent.push(data);
            origSend(data);
        };

        const conn = new RpcWebSocketConnection(clientWs, format.isBinary, format, clientHub.registry);
        const peer = new RpcClientPeer(clientHub, 'ws://test', false);
        peers.push(peer);
        clientHub.addPeer(peer);
        // Attach a connection WITHOUT completing a handshake — `_connection`
        // is set but the peer is not `_isConnected` yet (the run loop's
        // Handshaking window). This is exactly where a stray Cancel used to
        // corrupt the remote handshake.
        (peer as unknown as { setupConnection(c: unknown): void }).setupConnection(conn);

        const ac = new AbortController();
        const call = peer.call('DupService.inc:1', ['a'], { signal: ac.signal });
        call.result.promise.catch(() => { /* expected rejection */ });
        ac.abort();
        await delay(10);

        expect(sent.some(f => f.includes(RpcSystemCalls.cancel))).toBe(false);
        expect(peer.outboundCalls.get(call.callId)).toBeUndefined();
        await expect(call.result.promise).rejects.toThrow('Call cancelled');
    });

    it('R5: a stream started before the handshake completes sends no ack', async () => {
        const clientHub = new RpcHub('client-hub');
        hubs.push(clientHub);
        const format = RpcSerializationFormat.get('json5np');
        const [clientWs] = createMockWsPair();
        await delay(5); // let the mock socket reach OPEN

        const sent: string[] = [];
        const origSend = clientWs.send.bind(clientWs);
        clientWs.send = data => {
            if (typeof data === 'string') sent.push(data);
            origSend(data);
        };

        const conn = new RpcWebSocketConnection(clientWs, format.isBinary, format, clientHub.registry);
        const peer = new RpcClientPeer(clientHub, 'ws://test', false);
        peers.push(peer);
        clientHub.addPeer(peer);
        // Connection set, handshake NOT completed — same window as the Cancel case.
        (peer as unknown as { setupConnection(c: unknown): void }).setupConnection(conn);

        const ref = parseStreamRef(`${crypto.randomUUID()},7,30,61,1,0`);
        expect(ref).not.toBeNull();
        const stream = new RpcStream(ref!, peer);
        const iterator = stream[Symbol.asyncIterator]();
        iterator.next().catch(() => { /* resolved by return() below */ });
        await delay(10);
        await iterator.return?.();
        await delay(10);

        // Neither the lazy-start Ack nor the AckEnd may hit the wire before
        // $sys.Handshake — either would corrupt the remote handshake.
        expect(sent.some(f => f.includes('$sys.Ack'))).toBe(false);
    });

    it('R9: completed inbound calls are retained only up to the completed-calls limit', () => {
        const tracker = new RpcInboundCallTracker();
        tracker.completedCallsLimit = 2;
        for (const id of [1, 2, 3]) {
            const call = new RpcInboundCall(id, 'm', []);
            tracker.getOrRegister(call);
            call.setResult(() => { /* no-op resend */ });
            tracker.markCompleted(call);
        }

        expect(tracker.get(1)).toBeUndefined(); // oldest completed call is evicted
        expect(tracker.get(2)).toBeDefined();
        expect(tracker.get(3)).toBeDefined();
        expect(tracker.size).toBe(2);
    });

    it('R8: peer change is detected via RemotePeerId even when RemoteHubId is unchanged', async () => {
        // A single, stable server hub → stable hub id. Each reconnect creates a
        // fresh server peer (new RemotePeerId), mirroring .NET dropping an idle
        // RpcServerPeer and re-creating it on reconnect.
        const serverHub = new RpcHub('server-hub');
        const clientHub = new RpcHub('client-hub');
        hubs.push(serverHub, clientHub);

        const peer = new RpcClientPeer(clientHub, 'ws://test', false);
        peers.push(peer);
        clientHub.addPeer(peer);
        clientHub.reconnectDelayer.delays = RetryDelaySeq.fixed(20);

        let currentWs: FakeWebSocket | undefined;
        peer.webSocketFactory = () => {
            const channel = new MessageChannel();
            serverHub.getServerPeer(`server://${crypto.randomUUID()}`)
                .accept(new RpcMessageChannelConnection(channel.port2));
            currentWs = new FakeWebSocket(channel.port1);
            return currentWs;
        };

        let peerChangedCount = 0;
        peer.peerChanged.add(() => peerChangedCount++);

        peer.start();
        await peer.whenConnected();
        await delay(10);
        expect(peerChangedCount).toBe(0); // first handshake is never a change

        // Drop the socket → reconnect to the SAME hub id, but a NEW peer id.
        currentWs?.close(1001, 'drop');
        await waitFor(() => peerChangedCount === 1);
        expect(peerChangedCount).toBe(1);
    });

    it('R13: a premature disconnect keeps the reconnect backoff index growing', async () => {
        const serverHub = new RpcHub('server-hub');
        const clientHub = new RpcHub('client-hub');
        hubs.push(serverHub, clientHub);

        const peer = new RpcClientPeer(clientHub, 'ws://test', false);
        peers.push(peer);
        clientHub.addPeer(peer);
        peer.prematureDisconnectTimeoutMs = 60_000; // every quick drop is premature

        const tryIndexes: number[] = [];
        const delayer = clientHub.reconnectDelayer;
        delayer.delays = RetryDelaySeq.fixed(10);
        const origGetDelay = delayer.getDelay.bind(delayer);
        delayer.getDelay = (ti, sig) => {
            tryIndexes.push(ti);
            return origGetDelay(ti, sig);
        };

        let currentWs: FakeWebSocket | undefined;
        peer.webSocketFactory = () => {
            const channel = new MessageChannel();
            serverHub.getServerPeer(`server://${crypto.randomUUID()}`)
                .accept(new RpcMessageChannelConnection(channel.port2));
            currentWs = new FakeWebSocket(channel.port1);
            return currentWs;
        };

        peer.start();
        for (let i = 0; i < 4; i++) {
            await peer.whenConnected();
            await delay(5);
            currentWs?.close(1001, 'drop');
            await delay(40);
        }

        // Premature drops must let the index climb (…2, 3) instead of pinning
        // at 1 the way the old unconditional reset did.
        expect(Math.max(...tryIndexes)).toBeGreaterThan(1);
    });

    it('R13: a connection that outlives the threshold resets the backoff index', async () => {
        const serverHub = new RpcHub('server-hub');
        const clientHub = new RpcHub('client-hub');
        hubs.push(serverHub, clientHub);

        const peer = new RpcClientPeer(clientHub, 'ws://test', false);
        peers.push(peer);
        clientHub.addPeer(peer);
        peer.prematureDisconnectTimeoutMs = 0; // every disconnect counts as long-lived

        const tryIndexes: number[] = [];
        const delayer = clientHub.reconnectDelayer;
        delayer.delays = RetryDelaySeq.fixed(10);
        const origGetDelay = delayer.getDelay.bind(delayer);
        delayer.getDelay = (ti, sig) => {
            tryIndexes.push(ti);
            return origGetDelay(ti, sig);
        };

        let currentWs: FakeWebSocket | undefined;
        peer.webSocketFactory = () => {
            const channel = new MessageChannel();
            serverHub.getServerPeer(`server://${crypto.randomUUID()}`)
                .accept(new RpcMessageChannelConnection(channel.port2));
            currentWs = new FakeWebSocket(channel.port1);
            return currentWs;
        };

        peer.start();
        for (let i = 0; i < 4; i++) {
            await peer.whenConnected();
            await delay(5);
            currentWs?.close(1001, 'drop');
            await delay(40);
        }

        // A long-lived connection resets `_tryIndex`, so the delay index never
        // exceeds 1 across successive reconnects.
        expect(Math.max(...tryIndexes)).toBe(1);
    });

    it('R13: the default reconnect delayer backs off as the try index grows', () => {
        // The premature-disconnect fix only matters if a growing `_tryIndex`
        // yields growing delays — a fixed sequence would make it a no-op.
        const seq = new RpcClientPeerReconnectDelayer().delays;
        expect(seq.getDelay(6)).toBeGreaterThan(seq.getDelay(1) * 2);
        expect(seq.getDelay(100)).toBeLessThanOrEqual(11_000); // capped at ~10s
    });

    it('R16: a $sys.Reconnect with a stale handshake index is rejected', async () => {
        const serverHub = new RpcHub('server-hub');
        hubs.push(serverHub);
        const format = RpcSerializationFormat.get('json5np');
        const [, serverWs] = createMockWsPair();
        const serverConn = new RpcWebSocketConnection(serverWs, format.isBinary, format, serverHub.registry);
        const serverPeer: RpcServerPeer = serverHub.getServerPeer('server://test');
        serverPeer.serializationFormat = format;
        serverPeer.accept(serverConn);
        await delay(5);

        // Pin the server's own handshake index to a known generation.
        (serverPeer as unknown as { _ownHandshakeIndex: number })._ownHandshakeIndex = 5;

        const sent: string[] = [];
        const origSend = serverWs.send.bind(serverWs);
        serverWs.send = data => {
            if (typeof data === 'string') sent.push(data);
            origSend(data);
        };

        // Stale index (3 != 5) → $sys.Error (TooLateToReconnect), never $sys.Ok.
        serverHub.systemCallHandler.handle(
            { Method: RpcSystemCalls.reconnect, RelatedId: 42 }, [3, {}], serverPeer);
        await delay(5);
        expect(sent.some(f => f.includes(RpcSystemCalls.error) && f.includes('TooLateToReconnect'))).toBe(true);
        expect(sent.some(f => f.includes(`"RelatedId":42`) && f.includes(RpcSystemCalls.ok))).toBe(false);

        // Matching index (5) → normal $sys.Ok reply.
        sent.length = 0;
        serverHub.systemCallHandler.handle(
            { Method: RpcSystemCalls.reconnect, RelatedId: 43 }, [5, {}], serverPeer);
        await delay(5);
        expect(sent.some(f => f.includes(RpcSystemCalls.ok))).toBe(true);
    });
});
