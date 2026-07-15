// Regression tests for the "rpclife" batch of docs/plans/ts-port-audit.md:
//   R10 — unknown-object ack → $sys.Disconnect; remote stream fails fast
//   R11 — the three RpcStreamSender.onAck guards (host mismatch / not-started /
//         reset on a non-reconnectable sender)
//   R12 — per-peer outbound-call timeouts (command bounded, query unbounded)
//   R17 — $sys.Cancel aborts the running handler and suppresses its response
//   D4  — RpcLimits.Debug preset shape
//   Bonus — RpcClientPeer's stop signal unparks a pending reconnect delay

import { describe, it, expect } from 'vitest';
import { PromiseSource, RetryDelaySeq, withTimeout } from '@actuallab/core';
import {
    RpcHub,
    RpcClientPeer,
    RpcStream,
    RpcStreamSender,
    RpcLimits,
    RpcCallTimeouts,
    RpcSerializationFormat,
    createMessageChannelPair,
    defineRpcService,
    type RpcServerPeer,
    type RpcDispatchContext,
} from '../src/index.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

interface Pair {
    serverHub: RpcHub;
    clientHub: RpcHub;
    clientPeer: RpcClientPeer;
    serverPeer: RpcServerPeer;
    close(): void;
}

function connectedPair(limits?: RpcLimits): Pair {
    const serverHub = new RpcHub('server-hub');
    const clientHub = new RpcHub('client-hub');
    if (limits !== undefined) {
        serverHub.limits = limits;
        clientHub.limits = limits;
    }
    RpcSerializationFormat.get('json5np');
    const [a, b] = createMessageChannelPair();
    const clientPeer = new RpcClientPeer(clientHub, 'ws://test', false);
    clientPeer.connectWith(a);
    clientHub.addPeer(clientPeer);
    const serverPeer = serverHub.getServerPeer('server://test');
    serverPeer.accept(b);
    return {
        serverHub, clientHub, clientPeer, serverPeer,
        close() {
            serverHub.close();
            clientHub.close();
        },
    };
}

describe('D4 — RpcLimits.Debug', () => {
    it('relaxes keepAliveTimeout to 5 min and keeps every other field at its default', () => {
        const d = RpcLimits.Debug;
        const def = new RpcLimits();
        expect(d.keepAliveTimeoutMs).toBe(300_000);
        expect(d.keepAlivePeriodMs).toBe(def.keepAlivePeriodMs);
        expect(d.connectTimeoutMs).toBe(def.connectTimeoutMs);
        expect(d.handshakeTimeoutMs).toBe(def.handshakeTimeoutMs);
        expect(d.prematureDisconnectTimeoutMs).toBe(def.prematureDisconnectTimeoutMs);
        expect(d.completedInboundCallsLimit).toBe(def.completedInboundCallsLimit);
        expect(d.callTimeoutCheckPeriodMs).toBe(def.callTimeoutCheckPeriodMs);
    });
});

describe('R10 — unknown shared object → $sys.Disconnect', () => {
    it('acking an unknown shared object makes the server disconnect the remote stream (fail fast)', async () => {
        const pair = connectedPair();
        await delay(10);

        let disconnectCount = 0;
        const s = pair.serverHub.systemCallSender;
        const orig = s.disconnect.bind(s);
        s.disconnect = ((conn, format, ids) => {
            disconnectCount++;
            orig(conn, format, ids);
        }) as typeof s.disconnect;

        // A remote stream whose server-side sender does not exist. Enumerating
        // it sends the initial reset-ack for an unknown shared object.
        const ref = {
            hostId: 'h', localId: 4242, ackPeriod: 10, ackAdvance: 5,
            allowReconnect: true, isRealTime: false,
        };
        const stream = new RpcStream<string>(ref, pair.clientPeer);
        const iter = stream[Symbol.asyncIterator]();

        await expect(iter.next()).rejects.toThrow('Peer disconnected.');
        expect(disconnectCount).toBeGreaterThanOrEqual(1);

        pair.close();
    });
});

describe('R11 — RpcStreamSender.onAck guards', () => {
    function withSpy(pair: Pair): { count: () => number } {
        let n = 0;
        const s = pair.serverHub.systemCallSender;
        const orig = s.disconnect.bind(s);
        s.disconnect = ((conn, format, ids) => {
            n++;
            orig(conn, format, ids);
        }) as typeof s.disconnect;
        return { count: () => n };
    }

    function newSender(pair: Pair, allowReconnect = true): RpcStreamSender<string> {
        const sender = new RpcStreamSender<string>(
            pair.serverPeer, 256, 128, allowReconnect);
        pair.serverPeer.sharedObjects.register(sender);
        return sender;
    }

    it('host mismatch → disconnect', () => {
        const pair = connectedPair();
        const spy = withSpy(pair);
        const sender = newSender(pair);
        sender.onAck(0, '11111111-2222-3333-4444-555555555555');
        expect(spy.count()).toBe(1);
        pair.close();
    });

    it('a not-yet-started stream rejects any ack that is not mustReset && nextIndex === 0', () => {
        const pair = connectedPair();
        const spy = withSpy(pair);
        const sender = newSender(pair);
        // Plain flow-control ack (no reset) before the stream ever started.
        sender.onAck(0, '');
        expect(spy.count()).toBe(1);
        pair.close();
    });

    it('a reset ack on a non-reconnectable sender → disconnect (initial start is still allowed)', () => {
        const pair = connectedPair();
        const spy = withSpy(pair);
        const sender = newSender(pair, /* allowReconnect */ false);
        const hostId = sender.id.hostId;

        // Initial connect: mustReset && nextIndex === 0 — allowed, no disconnect.
        sender.onAck(0, hostId);
        expect(spy.count()).toBe(0);

        // A later reset (reconnect) ack on a non-reconnectable sender → disconnect.
        sender.onAck(2, hostId);
        expect(spy.count()).toBe(1);
        pair.close();
    });
});

describe('R12 — outbound call timeouts', () => {
    it('a command call rejects after its run timeout while a query keeps waiting', async () => {
        const limits = new RpcLimits({ callTimeoutCheckPeriodMs: 20 });
        const pair = connectedPair(limits);
        await delay(10);

        const Def = defineRpcService('Stuck', {
            query: { args: [] },
            command: { args: [], timeouts: new RpcCallTimeouts(Infinity, 100) },
        });
        pair.serverHub.addService(Def, {
            query: () => new Promise(() => { /* never resolves */ }),
            command: () => new Promise(() => { /* never resolves */ }),
        });
        interface StuckClient {
            query(): Promise<unknown>;
            command(): Promise<unknown>;
        }
        const svc = pair.clientHub.addClient<StuckClient>(pair.clientPeer, Def);

        const queryResult = svc.query();
        queryResult.catch(() => { /* rejected on close — observed */ });
        const commandResult = svc.command();

        await expect(commandResult).rejects.toThrow(/timed out/);

        // The query has no timeouts — it must still be pending.
        const status = await Promise.race([
            queryResult.then(() => 'settled', () => 'rejected'),
            delay(150).then(() => 'pending'),
        ]);
        expect(status).toBe('pending');

        pair.close();
    });

    it('a sent command does not run-timeout while the peer is disconnected', async () => {
        const limits = new RpcLimits({ callTimeoutCheckPeriodMs: 20 });
        const pair = connectedPair(limits);
        await delay(10);

        const Def = defineRpcService('StuckOffline', {
            command: { args: [], timeouts: new RpcCallTimeouts(Infinity, 100) },
        });
        pair.serverHub.addService(Def, {
            command: () => new Promise(() => { /* never resolves */ }),
        });
        const svc = pair.clientHub.addClient<{ command(): Promise<unknown> }>(pair.clientPeer, Def);

        const result = svc.command();
        result.catch(() => { /* rejected on close — observed */ });
        await delay(30);
        pair.clientPeer.disconnect();

        const status = await Promise.race([
            result.then(() => 'settled', () => 'rejected'),
            delay(250).then(() => 'pending'),
        ]);
        expect(status).toBe('pending');

        pair.close();
    });
});

describe('stop signal — close() during a pending reconnect delay', () => {
    it('unparks the reconnect loop so it exits promptly', async () => {
        const hub = new RpcHub('stop-signal-hub');
        hub.reconnectDelayer.delays = RetryDelaySeq.fixed(60_000);
        const peer = new RpcClientPeer(hub, 'ws://test', false);
        peer.webSocketFactory = () => { throw new Error('No network.'); };
        peer.start();
        while (peer.reconnectsAt === 0)
            await delay(5);

        peer.close();
        await withTimeout(peer.whenRunning!, 1_000, 'The reconnect loop did not exit.');
    });
});

describe('R17 — $sys.Cancel', () => {
    it('aborts the running handler and suppresses its response frame', async () => {
        const pair = connectedPair();
        await delay(10);

        const started = new PromiseSource<void>();
        let handlerAborted = false;
        const Def = defineRpcService('CancelSvc', {
            slow: { args: [], callTypeId: 1 },
        });
        pair.serverHub.addService(Def, {
            slow: async (context: unknown) => {
                const ctx = context as RpcDispatchContext;
                started.resolve();
                await new Promise<void>(resolve => {
                    ctx.signal?.addEventListener('abort', () => {
                        handlerAborted = true;
                        resolve();
                    });
                });
                return 'done';
            },
        });

        let okCount = 0;
        let errCount = 0;
        const s = pair.serverHub.systemCallSender;
        const origOk = s.ok.bind(s);
        const origErr = s.error.bind(s);
        s.ok = ((...a: Parameters<typeof s.ok>) => { okCount++; origOk(...a); }) as typeof s.ok;
        s.error = ((...a: Parameters<typeof s.error>) => { errCount++; origErr(...a); }) as typeof s.error;

        const ac = new AbortController();
        const call = pair.clientPeer.call('CancelSvc.slow:1', [], { callTypeId: 1, signal: ac.signal });
        call.result.promise.catch(() => { /* rejects with 'Call cancelled.' */ });

        await started;
        ac.abort();
        await delay(50);

        expect(handlerAborted).toBe(true);
        expect(okCount).toBe(0);
        expect(errCount).toBe(0);

        pair.close();
    });
});
