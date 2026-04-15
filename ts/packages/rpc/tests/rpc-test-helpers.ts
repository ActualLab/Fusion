import {
    RpcHub,
    RpcClientPeer,
    RpcWebSocketConnection,
    RpcSerializationFormat,
} from '../src/index.js';
import type { RpcServerPeer } from '../src/index.js';
import { createMockWsPair } from './mock-ws.js';

export interface TestHubPair {
    clientHub: RpcHub;
    serverHub: RpcHub;
    clientPeer: RpcClientPeer;
    serverPeer: RpcServerPeer;
}

export function createTestHubPair(formatKey: string): TestHubPair {
    const serverHub = new RpcHub('server-hub');
    const clientHub = new RpcHub('client-hub');
    const format = RpcSerializationFormat.get(formatKey);

    const [clientWs, serverWs] = createMockWsPair();
    const clientConn = new RpcWebSocketConnection(clientWs, format.isBinary, format, clientHub.registry);
    const serverConn = new RpcWebSocketConnection(serverWs, format.isBinary, format, serverHub.registry);

    const clientPeer = new RpcClientPeer(clientHub, 'ws://test', formatKey);
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    const serverPeer = serverHub.getServerPeer('server://test');
    serverPeer.accept(serverConn);

    return { clientHub, serverHub, clientPeer, serverPeer };
}

export const FORMATS = ['json5np', 'msgpack6', 'msgpack6c'] as const;

export function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}
