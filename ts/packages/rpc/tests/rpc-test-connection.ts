import {
    RpcHub,
    RpcClientPeer,
    type RpcServerPeer,
    RpcSerializationFormat,
    RpcWebSocketConnection,
    createMessageChannelPair,
} from '../src/index.js';
import { createMockWsPair } from './mock-ws.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

/**
 * Manages a paired client-server connection for testing — supports
 * connect/disconnect/reconnect/host-switch across all supported wire
 * formats.
 *
 * When `formatKey` is a text format (`json5np`), uses the lightweight
 * `RpcMessageChannelConnection` (MessagePort-based). For binary formats
 * (`msgpack6`, `msgpack6c`), uses a mock WebSocket pair wrapped in
 * `RpcWebSocketConnection` so the binary decoder runs.
 */
export class RpcTestConnection {
    clientHub: RpcHub;
    serverHub: RpcHub;
    clientPeer: RpcClientPeer;
    serverPeer: RpcServerPeer | undefined;
    /** Serialization format for this connection (default: `json5np`). */
    readonly formatKey: string;

    constructor(
        clientHub: RpcHub,
        serverHub: RpcHub,
        clientPeer: RpcClientPeer,
        formatKey = 'json5np',
    ) {
        this.clientHub = clientHub;
        this.serverHub = serverHub;
        this.clientPeer = clientPeer;
        this.formatKey = formatKey;
    }

    async connect(isPeerChanged = true): Promise<void> {
        const format = RpcSerializationFormat.get(this.formatKey);
        let clientConn, serverConn;
        if (format.isBinary) {
            const [clientWs, serverWs] = createMockWsPair();
            clientConn = new RpcWebSocketConnection(
                clientWs, format.isBinary, format, this.clientHub.registry);
            serverConn = new RpcWebSocketConnection(
                serverWs, format.isBinary, format, this.serverHub.registry);
        } else {
            const [a, b] = createMessageChannelPair();
            clientConn = a;
            serverConn = b;
        }

        this.clientPeer.connectWith(clientConn, isPeerChanged);

        if (isPeerChanged || this.serverPeer === undefined) {
            // New server peer — fresh identity.
            const ref = `server://${crypto.randomUUID()}`;
            this.serverPeer = this.serverHub.getServerPeer(ref);
            this.serverPeer.format = format;
            this.serverPeer.accept(serverConn);
        } else {
            // Same-peer reconnect: reuse the existing server peer so its
            // inbound tracker (live long-running calls) survives. The OLD
            // connection has already been closed; attach the new one.
            this.serverPeer.accept(serverConn);
        }

        // Yield a turn for handshakes + the client's $sys.Reconnect round-trip
        // (on same-peer reconnects) to complete before the caller observes state.
        await delay(10);
    }

    /** @param releaseServerPeer Drop the serverPeer reference so the next
     *  `connect()` creates a fresh peer. Set `false` for same-peer reconnects
     *  that must preserve the server's inbound tracker. */
    async disconnect(releaseServerPeer = true): Promise<void> {
        this.clientPeer.connection?.close();
        if (this.serverPeer !== undefined) {
            if (releaseServerPeer) {
                this.serverPeer.close();
                this.serverPeer = undefined;
            } else {
                // Close just the connection; keep the peer alive.
                this.serverPeer.connection?.close();
            }
        }
        await delay(1);
    }

    async reconnect(delayMs = 10, isPeerChanged = true): Promise<void> {
        await this.disconnect(/* releaseServerPeer */ isPeerChanged);
        await delay(delayMs);
        await this.connect(isPeerChanged);
    }

    /** Simulate a same-peer reconnect (e.g. transient network drop, server still alive).
     *  Preserves the server peer's identity and inbound-call tracker across the gap. */
    async reconnectSamePeer(delayMs = 10): Promise<void> {
        await this.reconnect(delayMs, false);
    }

    async switchHost(newServerHub: RpcHub): Promise<void> {
        this.serverHub = newServerHub;
        await this.reconnect();
    }
}

/** Background loop that repeatedly disconnects/reconnects at random intervals. */
export async function connectionDisruptor(
    conn: RpcTestConnection,
    signal: AbortSignal,
    connectedMs = [50, 150],
    disconnectedMs = [10, 40]
): Promise<void> {
    function randomInRange(range: number[]): number {
        const [min, max] = range;
        return min + Math.random() * (max - min);
    }

    while (!signal.aborted) {
        // Stay connected for a random duration
        await delay(randomInRange(connectedMs));
        // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
        if (signal.aborted) break;

        await conn.disconnect();

        // Stay disconnected for a random duration
        await delay(randomInRange(disconnectedMs));
        // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
        if (signal.aborted) break;

        await conn.connect();
    }
}
