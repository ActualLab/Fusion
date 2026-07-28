// Regression tests for review-r2 I14 — the `$sys.Reconnect` stale-generation
// check used to be `typeof handshakeIndex === 'number' && ...`, so a peer that
// sent the index as a string (or omitted it) skipped the check entirely.
import { describe, it, expect, afterEach } from 'vitest';
import {
    RpcHub,
    RpcWebSocketConnection,
    RpcSerializationFormat,
    RpcSystemCalls,
    type RpcServerPeer,
} from '../src/index.js';
import { createMockWsPair } from './mock-ws.js';
import { delay } from './rpc-test-helpers.js';

interface Harness {
    hub: RpcHub;
    peer: RpcServerPeer;
    sent: string[];
}

function createHarness(ownHandshakeIndex: number): Harness {
    const hub = new RpcHub('server-hub');
    const format = RpcSerializationFormat.get('json5np');
    const [, serverWs] = createMockWsPair();
    const conn = new RpcWebSocketConnection(serverWs, format.isBinary, format, hub.registry);
    const peer: RpcServerPeer = hub.getServerPeer('server://test');
    peer.serializationFormat = format;
    peer.accept(conn);
    (peer as unknown as { _ownHandshakeIndex: number })._ownHandshakeIndex = ownHandshakeIndex;

    const sent: string[] = [];
    const origSend = serverWs.send.bind(serverWs);
    serverWs.send = data => {
        if (typeof data === 'string') sent.push(data);
        origSend(data);
    };
    return { hub, peer, sent };
}

function isRejected(sent: string[]): boolean {
    return sent.some(f => f.includes(RpcSystemCalls.error) && f.includes('TooLateToReconnect'));
}

describe('$sys.Reconnect handshake-index guard (I14)', () => {
    let harness: Harness | undefined;

    afterEach(() => {
        harness?.hub.close();
        harness = undefined;
    });

    for (const [name, index] of [
        ['a string index', '5'],
        ['an omitted index', undefined],
        ['a null index', null],
        ['an object index', { Index: 5 }],
        ['a bigint index', BigInt(5)],
    ] as [string, unknown][]) {
        it(`rejects ${name} instead of skipping the check`, async () => {
            harness = createHarness(5);
            harness.hub.systemCallHandler.handle(
                { Method: RpcSystemCalls.reconnect, RelatedId: 42 }, [index, {}], harness.peer);
            await delay(5);

            expect(isRejected(harness.sent)).toBe(true);
            expect(harness.sent.some(f => f.includes(RpcSystemCalls.ok))).toBe(false);
        });
    }

    it('still accepts a matching numeric index', async () => {
        harness = createHarness(5);
        harness.hub.systemCallHandler.handle(
            { Method: RpcSystemCalls.reconnect, RelatedId: 43 }, [5, {}], harness.peer);
        await delay(5);

        expect(isRejected(harness.sent)).toBe(false);
        expect(harness.sent.some(f => f.includes(RpcSystemCalls.ok))).toBe(true);
    });
});
