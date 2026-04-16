import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
    RpcHub,
    RpcClientPeer,
    RpcRemoteExecutionMode,
    defineRpcService,
    IncreasingSeqCompressor,
} from '../src/index.js';
import { RpcTestConnection } from './rpc-test-connection.js';
import { FORMATS } from './rpc-test-helpers.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

/**
 * Regression tests for Bug 3: the TypeScript `$sys.Reconnect` protocol.
 *
 * Before this change the client would blindly re-send every in-flight
 * outbound call on every reconnect, causing the server to spawn a second
 * handler for streaming calls (e.g. ActualChat's PushAudio) and double-
 * process the stream.
 *
 * After the fix a same-peer reconnect first asks the server (via
 * `$sys.Reconnect:3`) which call IDs it no longer recognizes, and only
 * those get resent. Matches .NET `RpcOutboundCallTracker.Reconnect` at
 * src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:209-279.
 *
 * Parameterized over every supported TS wire format so the protocol's
 * client-side encoding and server-side `_handleReconnect` dispatch are
 * exercised on each serialization path.
 */

interface ISlowService {
    delayed(ms: number, marker: string): Promise<string>;
}

const SlowServiceDef = defineRpcService('SlowService', {
    delayed: {
        args: [0, ''],
        remoteExecutionMode:
            RpcRemoteExecutionMode.AwaitForConnection |
            RpcRemoteExecutionMode.AllowReconnect |
            RpcRemoteExecutionMode.AllowResend,
    },
});

describe.each(FORMATS)('RPC $sys.Reconnect protocol [%s]', (formatKey) => {
    let serverHub: RpcHub;
    let clientHub: RpcHub;
    let conn: RpcTestConnection;
    let invocationCounter: { n: number };

    beforeEach(async () => {
        serverHub = new RpcHub('server-hub');
        clientHub = new RpcHub('client-hub');
        invocationCounter = { n: 0 };

        serverHub.addService(SlowServiceDef, {
            delayed: async (msArg: unknown, marker: unknown) => {
                invocationCounter.n++;
                await delay(msArg as number);
                return `${String(marker)}:done`;
            },
        });

        const clientPeer = new RpcClientPeer(clientHub, 'ws://test', formatKey);
        clientHub.addPeer(clientPeer);

        conn = new RpcTestConnection(clientHub, serverHub, clientPeer, formatKey);
        await conn.connect();
    });

    afterEach(() => {
        serverHub.close();
        clientHub.close();
    });

    it('Test 3.2 — in-flight long-running call is NOT resent on same-peer reconnect', async () => {
        const client = clientHub.addClient<ISlowService>(conn.clientPeer, SlowServiceDef);

        // Kick off a 300ms call; hold the promise.
        const promise = client.delayed(300, 'hello');
        promise.catch(() => { /* prevent unhandled-rejection noise */ });

        // Let the server register the inbound call.
        await delay(20);
        expect(invocationCounter.n).toBe(1);

        // Same-peer reconnect. With the fix, the server's inbound tracker
        // retained the call; $sys.Reconnect reports it as known; client
        // skips the resend. With the OLD behavior the client would blindly
        // resend and the server would spawn a second handler, doubling the
        // invocation count.
        await conn.reconnectSamePeer();

        // Wait for the original call to complete.
        const result = await promise;
        expect(result).toBe('hello:done');

        // Count should have grown by ONE only — no duplicate invocation.
        expect(invocationCounter.n).toBe(1);
    });

    it('Test 3.4 — peer change resends eligible calls (unchanged path)', async () => {
        const client = clientHub.addClient<ISlowService>(conn.clientPeer, SlowServiceDef);

        const promise = client.delayed(200, 'hi');
        promise.catch(() => { /* noop */ });

        await delay(20);
        const countBefore = invocationCounter.n;
        expect(countBefore).toBe(1);

        // Swap hubs → peer-change detected → blind resend of AllowResend
        // calls. Invocation count grows.
        const altHub = new RpcHub('alt-server-hub');
        altHub.addService(SlowServiceDef, {
            delayed: async (msArg: unknown, marker: unknown) => {
                invocationCounter.n++;
                await delay(msArg as number);
                return `${String(marker)}:alt`;
            },
        });
        await conn.switchHost(altHub);

        const result = await promise;
        expect(result).toBe('hi:alt');
        expect(invocationCounter.n).toBeGreaterThan(countBefore);

        altHub.close();
    });

    it('Test 3.3 — a call the server has forgotten IS resent', async () => {
        const client = clientHub.addClient<ISlowService>(conn.clientPeer, SlowServiceDef);

        const promise = client.delayed(300, 'forget');
        promise.catch(() => { /* noop */ });

        await delay(20);
        expect(invocationCounter.n).toBe(1);

        // Disconnect with releaseServerPeer=true → next connect creates a
        // fresh server peer with an empty inbound tracker. From the client's
        // perspective it's still "same peer" (no handshake exchange in the
        // test harness), so $sys.Reconnect fires and the server answers
        // "I don't know this call ID" → client resends → invocation=2.
        await conn.disconnect(/* releaseServerPeer */ true);
        await conn.connect(/* isPeerChanged */ false);

        const result = await promise;
        expect(result).toBe('forget:done');
        expect(invocationCounter.n).toBe(2);
    });
});

describe('IncreasingSeqCompressor sanity-check for the reconnect wire format', () => {
    it('round-trips a sorted call-ID set', () => {
        const sortedIds = [1, 5, 10, 20, 100];
        const encoded = IncreasingSeqCompressor.serialize(sortedIds);
        expect(encoded.length).toBeGreaterThan(0);
        expect(IncreasingSeqCompressor.deserialize(encoded)).toEqual(sortedIds);
    });
});
