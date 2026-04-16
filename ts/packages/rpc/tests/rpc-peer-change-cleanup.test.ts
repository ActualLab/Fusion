import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
    RpcHub,
    RpcClientPeer,
    RpcStreamSender,
} from '../src/index.js';
import { RpcTestConnection } from './rpc-test-connection.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

/**
 * Regression tests for peer-change cleanup of client-owned shared objects
 * (Bug 2 in RPC_TS_FIXES).
 *
 * When a RpcClientPeer reconnects to a server with a different hubId
 * (peer change), any RpcStreamSender instances it owns (client→server
 * streams) must be disposed — the new server has no corresponding
 * remote-stream receiver, so ACKs would never arrive and the sender's
 * source enumerator would hang forever.
 *
 * Mirrors .NET RpcPeer.Reset() in src/ActualLab.Rpc/RpcPeer.cs:430-440.
 */
describe('RPC peer-change cleanup', () => {
    let serverHub: RpcHub;
    let clientHub: RpcHub;
    let conn: RpcTestConnection;

    beforeEach(async () => {
        serverHub = new RpcHub('server-hub');
        clientHub = new RpcHub('client-hub');

        const clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientHub.addPeer(clientPeer);

        conn = new RpcTestConnection(clientHub, serverHub, clientPeer);
        await conn.connect();
    });

    afterEach(() => {
        serverHub.close();
        clientHub.close();
    });

    it('should dispose client-owned shared senders on peer change (Test 2.1)', async () => {
        // Register a client-owned stream sender on the client peer
        // (simulates a client→server stream like ActualChat's PushAudio).
        const sender = new RpcStreamSender<number>(conn.clientPeer);
        conn.clientPeer.sharedObjects.register(sender);

        let finalizeCalled = false;
        async function* source(signal: AbortSignal): AsyncIterable<number> {
            try {
                for (let i = 0; !signal.aborted; i++) {
                    yield i;
                    await delay(5);
                }
            } finally {
                finalizeCalled = true;
            }
        }
        void sender.writeFrom(source(sender.abortSignal));
        // Trigger the initial ack-gate so writeFrom begins pumping
        sender.onAck(0, sender.id.hostId);

        await delay(20);
        expect(finalizeCalled).toBe(false);
        expect([...conn.clientPeer.sharedObjects.keys()].length).toBe(1);

        // Switch to a different server hub — triggers peer-change path
        // in _reconnect() (connectWith with isPeerChanged=true, which is
        // the test harness default).
        const otherServerHub = new RpcHub('other-server-hub');
        await conn.switchHost(otherServerHub);

        // Give the AbortSignal a chance to propagate and the grace period
        // for force-close to run.
        await delay(150);

        expect([...conn.clientPeer.sharedObjects.keys()].length).toBe(0);
        expect(finalizeCalled).toBe(true);

        otherServerHub.close();
    });

    it('should NOT dispose client-owned shared senders on same-peer reconnect (Test 2.2)', async () => {
        // This is the regression guard: when the same server is reachable again
        // (transient network drop), senders must survive so the stream can
        // resume via the existing Ack/reset protocol.
        const sender = new RpcStreamSender<number>(conn.clientPeer);
        conn.clientPeer.sharedObjects.register(sender);

        let finalizeCalled = false;
        async function* source(signal: AbortSignal): AsyncIterable<number> {
            try {
                for (let i = 0; !signal.aborted; i++) {
                    yield i;
                    await delay(5);
                }
            } finally {
                finalizeCalled = true;
            }
        }
        void sender.writeFrom(source(sender.abortSignal));
        sender.onAck(0, sender.id.hostId);

        await delay(20);
        expect(finalizeCalled).toBe(false);

        // Same-peer reconnect — uses isPeerChanged=false path
        await conn.reconnectSamePeer();
        await delay(20);

        expect([...conn.clientPeer.sharedObjects.keys()].length).toBe(1);
        expect(finalizeCalled).toBe(false);

        // Cleanup
        sender.disconnect();
        await delay(150);
    });

    it('should dispose multiple shared senders on peer change', async () => {
        const senders: RpcStreamSender<number>[] = [];
        const finalized: boolean[] = [];
        const count = 3;

        for (let k = 0; k < count; k++) {
            const sender = new RpcStreamSender<number>(conn.clientPeer);
            conn.clientPeer.sharedObjects.register(sender);
            senders.push(sender);
            finalized.push(false);

            const idx = k;
            async function* source(signal: AbortSignal): AsyncIterable<number> {
                try {
                    for (let i = 0; !signal.aborted; i++) {
                        yield i;
                        await delay(5);
                    }
                } finally {
                    finalized[idx] = true;
                }
            }
            void sender.writeFrom(source(sender.abortSignal));
            sender.onAck(0, sender.id.hostId);
        }

        await delay(20);
        expect(finalized.every(x => !x)).toBe(true);
        expect([...conn.clientPeer.sharedObjects.keys()].length).toBe(count);

        const otherServerHub = new RpcHub('other-server-hub-multi');
        await conn.switchHost(otherServerHub);
        await delay(150);

        expect([...conn.clientPeer.sharedObjects.keys()].length).toBe(0);
        expect(finalized.every(x => x)).toBe(true);

        otherServerHub.close();
    });
});
