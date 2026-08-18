// $sys.Reconnect is a once-per-connection call: a repeat is a replay, and the
// reconciliation it performs is proportional to the call ids it carries.
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
    return sent.some(f => f.includes(RpcSystemCalls.error) && f.includes('already reconnected'));
}

async function sendReconnect(harness: Harness, relatedId: number): Promise<void> {
    harness.hub.systemCallHandler.handle(
        { Method: RpcSystemCalls.reconnect, RelatedId: relatedId },
        [(harness.peer as unknown as { _ownHandshakeIndex: number })._ownHandshakeIndex, {}],
        harness.peer);
    await delay(5);
}

describe('$sys.Reconnect single-shot gate', () => {
    let harness: Harness | undefined;

    afterEach(() => {
        harness?.hub.close();
        harness = undefined;
    });

    it('accepts the first Reconnect and rejects every repeat', async () => {
        harness = createHarness(5);

        await sendReconnect(harness, 1);
        expect(isRejected(harness.sent)).toBe(false);
        expect(harness.sent.some(f => f.includes(RpcSystemCalls.ok))).toBe(true);

        harness.sent.length = 0;
        await sendReconnect(harness, 2);
        expect(isRejected(harness.sent)).toBe(true);
        expect(harness.sent.some(f => f.includes(RpcSystemCalls.ok))).toBe(false);

        harness.sent.length = 0;
        await sendReconnect(harness, 3);
        expect(isRejected(harness.sent)).toBe(true);
    });

    it('gives the next connection generation its own allowance', async () => {
        harness = createHarness(5);

        await sendReconnect(harness, 1);
        expect(isRejected(harness.sent)).toBe(false);

        // A new handshake generation resets the claim, just like a new
        // RpcPeerConnectionState does on the .NET side
        (harness.peer as unknown as { nextOwnHandshakeIndex(): number }).nextOwnHandshakeIndex();
        harness.sent.length = 0;
        await sendReconnect(harness, 2);
        expect(isRejected(harness.sent)).toBe(false);
        expect(harness.sent.some(f => f.includes(RpcSystemCalls.ok))).toBe(true);
    });
});
