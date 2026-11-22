import {
    RpcHub,
    RpcClientPeer,
    RpcWebSocketConnection,
    RpcWireFormat,
} from '../src/index.js';
import type { RpcConnection, RpcServerPeer } from '../src/index.js';
import { createMockWsPair } from './mock-ws.js';

export interface TestHubPair {
    clientHub: RpcHub;
    serverHub: RpcHub;
    clientPeer: RpcClientPeer;
    serverPeer: RpcServerPeer;
}

export function createTestHubPair(wireFormatKey: string): TestHubPair {
    const serverHub = new RpcHub('server-hub');
    const clientHub = new RpcHub('client-hub');
    const wireFormat = RpcWireFormat.get(wireFormatKey);

    const [clientWs, serverWs] = createMockWsPair();
    const clientConn = new RpcWebSocketConnection(clientWs, wireFormat.isBinary, wireFormat, clientHub.registry);
    const serverConn = new RpcWebSocketConnection(serverWs, wireFormat.isBinary, wireFormat, serverHub.registry);

    const clientPeer = new RpcClientPeer(clientHub, 'ws://test', wireFormatKey);
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    const serverPeer = serverHub.getServerPeer('server://test');
    serverPeer.accept(serverConn);

    return { clientHub, serverHub, clientPeer, serverPeer };
}

/** Drives a freshly `accept()`ed server peer out of `Handshaking`. Without it the
 *  peer keeps `connection` set while `isConnected` stays false, and every
 *  non-handshake send it makes — stream items, acks — is gated off.
 *  Reaching `Connected` also arms the keep-alive sender and the silence
 *  watchdog, so a test that runs past `keepAliveTimeoutMs` (or advances fake
 *  timers that far) will see the server force-close the socket. */
export function sendClientHandshake(
    clientHub: RpcHub,
    clientPeer: RpcClientPeer,
    clientConn: RpcConnection,
    index = 1,
): void {
    clientHub.systemCallSender.handshake(
        clientConn, clientPeer.wireFormat, clientPeer.id, clientHub.hubId, index);
}

export const FORMATS = ['json5np', 'msgpack6', 'msgpack6c'] as const;

export function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}
