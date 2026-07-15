import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { AsyncContext } from '@actuallab/core';
import { Computed, MutableState } from '@actuallab/fusion';
import {
    RpcClientPeer,
    RpcServerPeer,
    RpcType,
    RpcSystemCalls,
    IncreasingSeqCompressor,
    defineRpcService,
    createMessageChannelPair,
    splitFrame,
    deserializeMessage,
    type RpcConnection,
} from '@actuallab/rpc';
import {
    FusionHub,
    RpcOutboundComputeCall,
    defineComputeService,
} from '../src/index.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

const CounterServiceDef = defineComputeService('CounterService', {
    getCount: { args: [''] },
});
const MutationServiceDef = defineRpcService('MutationService', {
    setCount: { args: ['', 0], returns: RpcType.noWait },
});
const HangingServiceDef = defineRpcService('HangingService', {
    hang: { args: [''] },
});

interface ServerHub {
    hub: FusionHub;
    store: Map<string, MutableState<number>>;
    getCountCalls: () => number;
}

function createServerHub(): ServerHub {
    const hub = new FusionHub();
    const store = new Map<string, MutableState<number>>();
    let getCountCalls = 0;

    function getState(key: string): MutableState<number> {
        let s = store.get(key);
        if (s === undefined) {
            s = new MutableState(0);
            store.set(key, s);
        }
        return s;
    }

    hub.addService(CounterServiceDef, {
        getCount(key: unknown): number {
            getCountCalls++;
            return getState(key as string).use();
        },
    });
    hub.addService(MutationServiceDef, {
        setCount(key: unknown, value: unknown) {
            getState(key as string).set(value as number);
        },
    });
    hub.addService(HangingServiceDef, {
        hang(): Promise<number> {
            // Never resolves — exercises an in-flight regular call across reconnect.
            return new Promise<number>(() => undefined);
        },
    });

    return { hub, store, getCountCalls: () => getCountCalls };
}

// Wraps the connection's `send` and collects every call id reported in a
// `$sys.Reconnect` frame (across all completedStage groups).
function captureReconnectedIds(conn: RpcConnection): () => number[] {
    const ids: number[] = [];
    const original = conn.send.bind(conn);
    conn.send = (data: string) => {
        for (const raw of splitFrame(data)) {
            if (raw.length === 0) continue;
            const { message, args } = deserializeMessage(raw);
            if (message.Method !== RpcSystemCalls.reconnect) continue;
            const stages = args[1] as Record<string, string>;
            for (const b64 of Object.values(stages)) {
                const bytes = Uint8Array.from(atob(b64), c => c.charCodeAt(0));
                for (const id of IncreasingSeqCompressor.deserialize(bytes))
                    ids.push(id);
            }
        }
        original(data);
    };
    return () => ids;
}

describe('Fusion-over-RPC glue fixes', () => {
    let server: ServerHub;
    let clientHub: FusionHub;

    beforeEach(() => {
        AsyncContext.current = undefined;
        server = createServerHub();
        clientHub = new FusionHub('client');
    });

    afterEach(() => {
        server.hub.close();
        clientHub.close();
    });

    // F6
    it('excludes stage-3 compute calls from $sys.Reconnect while regular in-flight calls reconcile', async () => {
        const [clientConn1, serverConn1] = createMessageChannelPair();
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(clientConn1);
        clientHub.addPeer(clientPeer);
        const serverPeer: RpcServerPeer = server.hub.acceptRpcConnection(serverConn1);
        await delay(10);

        // Stage-3 compute call: result resolved, kept registered.
        const computeCall = clientPeer.call('CounterService.getCount:2', ['x'], {
            callTypeId: 1,
            outboundCallFactory: (id, m) => new RpcOutboundComputeCall(id, m),
        }) as RpcOutboundComputeCall;
        await computeCall.result.promise;

        // Regular in-flight call: server method hangs, so it never completes.
        const hangingCall = clientPeer.call('HangingService.hang:2', ['y']);
        await delay(10);
        expect(hangingCall.result.isCompleted).toBe(false);

        // Same-peer reconnect: capture the $sys.Reconnect frame the client sends.
        const [clientConn2, serverConn2] = createMessageChannelPair();
        const reportedIds = captureReconnectedIds(clientConn2);
        serverPeer.accept(serverConn2);
        clientPeer.connectWith(clientConn2, false);
        await delay(30);

        expect(reportedIds()).toContain(hangingCall.callId);
        expect(reportedIds()).not.toContain(computeCall.callId);
        // The invalidate-on-reconnect behavior is preserved.
        expect(computeCall.whenInvalidated.isCompleted).toBe(true);

        clientConn1.close();
        clientConn2.close();
    });

    // F8
    it('returns the same client proxy per (peer, service) and shares one RPC call across consumers', async () => {
        const [clientConn, serverConn] = createMessageChannelPair();
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        server.hub.acceptRpcConnection(serverConn);
        await delay(10);

        const c1 = clientHub.addClient<{ getCount(key: string): Promise<number> }>(
            clientPeer, CounterServiceDef);
        const c2 = clientHub.addClient<{ getCount(key: string): Promise<number> }>(
            clientPeer, CounterServiceDef);
        expect(c1).toBe(c2);

        const [a, b] = await Promise.all([
            Computed.capture(() => c1.getCount('x')),
            Computed.capture(() => c2.getCount('x')),
        ]);
        expect(a.value).toBe(0);
        expect(b.value).toBe(0);
        // Shared key space → one computed, one server-side invocation.
        expect(a).toBe(b);
        expect(server.getCountCalls()).toBe(1);

        clientConn.close();
    });

    it('mints distinct proxies for different peers', async () => {
        const [clientConnA, serverConnA] = createMessageChannelPair();
        const [clientConnB, serverConnB] = createMessageChannelPair();
        const peerA = new RpcClientPeer(clientHub, 'ws://a');
        const peerB = new RpcClientPeer(clientHub, 'ws://b');
        peerA.connectWith(clientConnA);
        peerB.connectWith(clientConnB);
        clientHub.addPeer(peerA);
        clientHub.addPeer(peerB);
        server.hub.acceptRpcConnection(serverConnA);
        server.hub.acceptRpcConnection(serverConnB);
        await delay(10);

        const cA = clientHub.addClient(peerA, CounterServiceDef);
        const cB = clientHub.addClient(peerB, CounterServiceDef);
        expect(cA).not.toBe(cB);

        clientConnA.close();
        clientConnB.close();
    });
});
