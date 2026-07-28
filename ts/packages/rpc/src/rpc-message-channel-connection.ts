// .NET counterpart: none — this is a TS-only test transport.
//
// .NET tests use a different mechanism: RpcTestClient creates an in-memory
// Channel<RpcMessage> pair wrapped in an RpcTransport.  We use the browser-
// standard MessageChannel API instead, which gives us a synchronous in-process
// transport without requiring WebSocket mocks.

import { EventHandlerSet } from '@actuallab/core';
import { getLogs } from './logging.js';
import { splitFrame } from './rpc-serialization.js';
import type { RpcConnection, RpcReceivedMessage } from './rpc-connection.js';

const { warnLog } = getLogs('RpcMessageChannelConnection');

/** MessagePort-based RpcConnection — for in-process testing without WebSocket mocks. */
export class RpcMessageChannelConnection implements RpcConnection {
    private _port: MessagePort;
    private _open = true;

    readonly binaryMode = false;
    readonly messageReceived = new EventHandlerSet<RpcReceivedMessage>();
    readonly closed = new EventHandlerSet<{ code: number; reason: string }>();
    readonly whenConnected: Promise<void> = Promise.resolve(); // immediately connected

    constructor(port: MessagePort) {
        this._port = port;
        port.onmessage = (ev: MessageEvent) => {
            const data =
                typeof ev.data === 'string' ? ev.data : String(ev.data);
            // Isolated per message: one bad message must not drop its frame-mates,
            // and an escaping error would be an uncaught exception under Node.
            for (const msg of splitFrame(data))
                if (msg.length > 0)
                    this.messageReceived.triggerSafe(
                        { kind: 'text', raw: msg },
                        e => warnLog?.log('Failed to handle a text message:', e));
        };
    }

    get isOpen(): boolean {
        return this._open;
    }

    send(serializedMessage: string): void {
        if (!this._open) return;
        try {
            this._port.postMessage(serializedMessage);
        } catch {
            // never fail
        }
    }

    sendBinary(data: Uint8Array): void {
        if (!this._open) return;
        try {
            this._port.postMessage(data, [data.buffer]);
        } catch {
            // never fail
        }
    }

    close(code?: number, reason?: string): void {
        if (!this._open) return;
        this._open = false;
        this._port.close();
        this.closed.trigger({ code: code ?? 1000, reason: reason ?? '' });
    }
}

/** Creates a pair of connected RpcMessageChannelConnections for testing. */
export function createMessageChannelPair(): [
    RpcMessageChannelConnection,
    RpcMessageChannelConnection,
    ] {
    const channel = new MessageChannel();
    return [
        new RpcMessageChannelConnection(channel.port1),
        new RpcMessageChannelConnection(channel.port2),
    ];
}
