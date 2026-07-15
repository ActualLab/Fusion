import { describe, it, expect } from 'vitest';
import {
    RpcHub,
    RpcLimits,
    createMessageChannelPair,
} from '../src/index.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

describe('RpcServerPeer lifecycle (F11)', () => {
    it('removes a server peer from hub.peers after its connection closes + close timeout', async () => {
        const hub = new RpcHub();
        hub.limits = new RpcLimits({ serverPeerCloseTimeoutMs: 50 });
        const [clientConn, serverConn] = createMessageChannelPair();
        const ref = 'server://lifecycle-1';
        const peer = hub.getServerPeer(ref);
        peer.accept(serverConn);
        await delay(10);
        expect(hub.peers.has(ref)).toBe(true);

        serverConn.close();
        await delay(20); // still within the 50ms close window
        expect(hub.peers.has(ref)).toBe(true);

        await delay(70); // past the window
        expect(hub.peers.has(ref)).toBe(false);

        clientConn.close();
        hub.close();
    });

    it('keeps the server peer when re-accepted within the close window (same-peer reconnect)', async () => {
        const hub = new RpcHub();
        hub.limits = new RpcLimits({ serverPeerCloseTimeoutMs: 80 });
        const [clientConn1, serverConn1] = createMessageChannelPair();
        const ref = 'server://lifecycle-2';
        const peer = hub.getServerPeer(ref);
        peer.accept(serverConn1);
        await delay(10);

        serverConn1.close();
        await delay(30); // within the window

        const [clientConn2, serverConn2] = createMessageChannelPair();
        peer.accept(serverConn2); // re-accept the SAME peer → cancels removal
        await delay(90); // well past the original 80ms window
        expect(hub.peers.has(ref)).toBe(true);

        clientConn1.close();
        clientConn2.close();
        hub.close();
    });

    it('close() while connected leaves no stale close timer that evicts a same-ref successor', async () => {
        const hub = new RpcHub();
        hub.limits = new RpcLimits({ serverPeerCloseTimeoutMs: 40 });
        const [clientConn, serverConn] = createMessageChannelPair();
        const ref = 'server://lifecycle-3';
        const peer = hub.getServerPeer(ref);
        peer.accept(serverConn);
        await delay(10);

        peer.close(); // fires conn.closed on this peer — must not re-arm its close timer
        expect(hub.peers.has(ref)).toBe(false);

        const successor = hub.getServerPeer(ref);
        await delay(70); // past the closed peer's would-be timer window
        expect(hub.peers.get(ref)).toBe(successor);

        clientConn.close();
        hub.close();
    });
});
