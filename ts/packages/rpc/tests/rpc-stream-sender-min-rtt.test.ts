import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
    RpcHub,
    RpcClientPeer,
    RpcStreamSender,
    createMessageChannelPair,
} from '../src/index.js';
import { delay } from './rpc-test-helpers.js';

const RTT_UPPER_BOUND_MS = 10_000;

/**
 * Smoke tests for `RpcStreamSender.minRttMs` — a windowed minimum of
 * send→ack round-trip samples. Drives `sendItem`/`onAck` directly (both
 * public) rather than a full stream E2E.
 */
describe('RpcStreamSender min-RTT', () => {
    let clientHub: RpcHub;
    let clientPeer: RpcClientPeer;

    beforeEach(() => {
        clientHub = new RpcHub('client-hub');
        const [cc] = createMessageChannelPair();
        clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(cc);
        clientHub.addPeer(clientPeer);
    });

    afterEach(() => {
        clientHub.close();
    });

    function newStartedSender(): RpcStreamSender<number> {
        const sender = new RpcStreamSender<number>(clientPeer, 2, 4);
        clientPeer.sharedObjects.register(sender);
        // Initial connect ack (mustReset, hostId === our hostId) starts the stream.
        sender.onAck(0, clientHub.hubId);
        return sender;
    }

    it('reports -1 until a round trip is sampled', () => {
        const sender = newStartedSender();
        expect(sender.minRttMs).toBe(-1);
    });

    it('records a sample once an ack acknowledges sent items', async () => {
        const sender = newStartedSender();
        sender.sendItem(10);
        sender.sendItem(20);
        await delay(15);
        // Regular ack (hostId '' → mustReset false) covering both sent items.
        sender.onAck(2, '');
        expect(sender.minRttMs).toBeGreaterThanOrEqual(0);
        expect(sender.minRttMs).toBeLessThan(RTT_UPPER_BOUND_MS);
    });

    it('a mustReset ack clears pending send times without sampling', async () => {
        const sender = newStartedSender();
        sender.sendItem(10);
        await delay(10);
        // Reset ack (hostId set) drops in-flight timing; still no sample.
        sender.onAck(1, clientHub.hubId);
        expect(sender.minRttMs).toBe(-1);
    });
});
