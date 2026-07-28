// Regression tests for review-r2 I8 — `$sys.Disconnect` carries ids from the
// sender's shared-object namespace, i.e. our *remote* one. Resolving them
// against `peer.sharedObjects` too (both counters start at 1) used to abort
// unrelated outgoing senders.
import { describe, it, expect, afterEach } from 'vitest';
import {
    RpcStream,
    RpcStreamSender,
    RpcObjectKind,
    RpcSystemCalls,
    parseStreamRef,
    type IRpcObject,
    type RpcMessage,
} from '../src/index.js';
import { createTestHubPair } from './rpc-test-helpers.js';
import type { TestHubPair } from './rpc-test-helpers.js';

class SpyRemoteObject implements IRpcObject {
    readonly kind = RpcObjectKind.Remote;
    readonly allowReconnect = true;
    disconnectCount = 0;

    constructor(readonly id: { hostId: string; localId: number }) { }

    reconnect(): void { /* no-op */ }
    disconnect(): void {
        this.disconnectCount++;
    }
}

function sendDisconnect(pair: TestHubPair, ids: number[]): void {
    const message: RpcMessage = { Method: RpcSystemCalls.disconnect, RelatedId: 0 };
    pair.clientHub.systemCallHandler.handle(message, [ids], pair.clientPeer);
}

describe('$sys.Disconnect id namespace (I8)', () => {
    let pair: TestHubPair | undefined;

    afterEach(() => {
        pair?.serverHub.close();
        pair?.clientHub.close();
        pair = undefined;
    });

    it('does not abort an outgoing sender that shares the id of the disconnected remote object', () => {
        pair = createTestHubPair('json5np');
        const peer = pair.clientPeer;

        // Outgoing (shared) stream sender — its localId is 1, the first id the
        // shared-object counter hands out.
        const sender = new RpcStreamSender<number>(peer);
        peer.sharedObjects.register(sender);
        expect(sender.id.localId).toBe(1);

        // Incoming (remote) stream with the colliding localId 1.
        const remote = new SpyRemoteObject({ hostId: 'server-host', localId: 1 });
        peer.remoteObjects.register(remote);

        sendDisconnect(pair, [1]);

        expect(remote.disconnectCount).toBe(1);
        expect(sender.abortSignal.aborted).toBe(false);
        expect(peer.sharedObjects.get(1)).toBe(sender);
    });

    it('leaves every outgoing sender alive under a $sys.Disconnect id sweep', () => {
        pair = createTestHubPair('json5np');
        const peer = pair.clientPeer;

        const senders = [1, 2, 3].map(() => {
            const sender = new RpcStreamSender<number>(peer);
            peer.sharedObjects.register(sender);
            return sender;
        });

        sendDisconnect(pair, [1, 2, 3, 4, 5]);

        for (const sender of senders)
            expect(sender.abortSignal.aborted).toBe(false);
        expect([...peer.sharedObjects.keys()]).toEqual([1, 2, 3]);
    });

    it('still tears down the remote stream the ids actually address', () => {
        pair = createTestHubPair('json5np');
        const peer = pair.clientPeer;

        const ref = parseStreamRef(`${crypto.randomUUID()},1,30,61,1,0`)!;
        const stream = new RpcStream<number>(ref, peer);
        peer.remoteObjects.register(stream);

        const iterator = stream[Symbol.asyncIterator]();
        const whenNext = iterator.next();
        sendDisconnect(pair, [1]);

        return expect(whenNext).rejects.toThrow('Peer disconnected.');
    });
});
