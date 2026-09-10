// Every non-handshake frame is gated on `RpcPeer.wireConnection` (the port of
// C#'s `RpcPeer.Transport`, RpcPeer.cs:29-52). `_connection` is set before the
// socket opens and stays set through the handshake, and `RpcConnection` buffers
// pre-OPEN sends, so writing through it during Connecting/Handshaking puts the
// frame on the wire ahead of $sys.Handshake and the remote rejects the
// connection ("Handshake failed: expected $sys.Handshake, got ...").
//
// The stream half of this lives in rpc-stream-sender-handshake-gate.test.ts;
// this file covers the peer-level writers whose send time is decoupled from the
// frame that triggered them — deferred inbound results and the keep-alive timer.
import { describe, it, expect, afterEach } from 'vitest';
import {
    RpcHub,
    RpcClientPeer,
    RpcSerializationFormat,
    RpcSystemCalls,
    RpcWebSocketConnection,
    IncreasingSeqCompressor,
    defineRpcService,
    type RpcServerPeer,
} from '../src/index.js';
import { createMockWsPair } from './mock-ws.js';
import { delay } from './rpc-test-helpers.js';

const SvcDef = defineRpcService('GateSvc', {
    echo: { args: [''] },
    boom: { args: [''] },
});

function base64(bytes: Uint8Array): string {
    let s = '';
    for (const b of bytes) s += String.fromCharCode(b);
    return btoa(s);
}

describe('pre-handshake wire writes', () => {
    const hubs: RpcHub[] = [];
    const format = RpcSerializationFormat.get('json5np');

    afterEach(() => {
        for (const h of hubs.splice(0)) h.close();
    });

    function newServerHub(): RpcHub {
        const hub = new RpcHub('server-hub');
        hubs.push(hub);
        hub.addService(SvcDef, {
            echo: (k: unknown) => `echo:${k as string}`,
            boom: (_k: unknown) => {
                throw new Error('handler blew up');
            },
        });
        return hub;
    }

    /** Captures the raw call frame a real client sends for `method`. */
    async function captureCallFrame(serverHub: RpcHub, method: 'echo' | 'boom'): Promise<string> {
        const clientHub = new RpcHub('client-hub');
        hubs.push(clientHub);
        const [clientWs, serverWs] = createMockWsPair();
        const clientConn = new RpcWebSocketConnection(
            clientWs, format.isBinary, format, clientHub.registry);
        const serverConn = new RpcWebSocketConnection(
            serverWs, format.isBinary, format, serverHub.registry);

        const frames: string[] = [];
        const origSend = clientWs.send.bind(clientWs);
        clientWs.send = data => {
            if (typeof data === 'string') frames.push(data);
            origSend(data);
        };

        const clientPeer = new RpcClientPeer(clientHub, 'ws://capture', 'json5np');
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        serverHub.getServerPeer('server://capture').accept(serverConn);
        await delay(10);

        const client = clientHub.addClient<Record<string, (k: string) => Promise<unknown>>>(
            clientPeer, SvcDef);
        // `boom` rejects by design; we only want the frame it produced.
        await client[method]('k').catch(() => undefined);
        await delay(10);

        const frame = frames.find(f => f.includes('GateSvc.' + method));
        expect(frame).toBeDefined();
        return frame!;
    }

    /** A server peer parked in `Handshaking`: connection live, `isConnected` false. */
    async function handshakingServerPeer(serverHub: RpcHub, ref: string) {
        const [clientWs, serverWs] = createMockWsPair();
        const serverConn = new RpcWebSocketConnection(
            serverWs, format.isBinary, format, serverHub.registry);
        const clientConn = new RpcWebSocketConnection(
            clientWs, format.isBinary, format, serverHub.registry);
        const peer: RpcServerPeer = serverHub.getServerPeer(ref);
        peer.serializationFormat = format;
        peer.accept(serverConn);

        const sent: string[] = [];
        const origSend = serverWs.send.bind(serverWs);
        serverWs.send = data => {
            if (typeof data === 'string') sent.push(data);
            origSend(data);
        };
        await delay(5); // Let the mock sockets reach OPEN.
        return { peer, sent, clientWs, clientConn };
    }

    const cases: [string, 'echo' | 'boom', string][] = [
        ['result', 'echo', RpcSystemCalls.ok],
        ['error', 'boom', RpcSystemCalls.error],
    ];
    for (const [name, method, replyKind] of cases) {
        it('holds an inbound call ' + name + ' written before the handshake completes', async () => {
            const serverHub = newServerHub();
            const frame = await captureCallFrame(serverHub, method);
            const h = await handshakingServerPeer(serverHub, 'server://gate');

            expect(h.peer.connection).toBeDefined();
            expect(h.peer.isConnected).toBe(false);

            // Deliver the call. The handler runs and its `send` closure fires
            // while the peer is still handshaking.
            h.clientWs.send(frame);
            await delay(20);

            expect(h.sent.some(f => f.includes(replyKind))).toBe(false);
        });
    }

    it('re-sends a held result once the client reconciles after the handshake', async () => {
        const serverHub = newServerHub();
        const frame = await captureCallFrame(serverHub, 'echo');
        const h = await handshakingServerPeer(serverHub, 'server://gate');

        h.clientWs.send(frame);
        await delay(20);
        expect(h.sent.some(f => f.includes(RpcSystemCalls.ok))).toBe(false);

        // The dropped frame is not lost: $sys.Reconnect reports the call at
        // stage 0 ("no result"), and `resendResult` puts it back on the wire.
        const callId = Number(/"RelatedId":(\d+)/.exec(frame)?.[1]);
        expect(Number.isSafeInteger(callId)).toBe(true);

        serverHub.systemCallSender.handshake(
            h.clientConn, format, 'client-peer', serverHub.hubId, 1);
        await delay(10);
        expect(h.peer.isConnected).toBe(true);

        h.sent.length = 0;
        serverHub.systemCallHandler.handle(
            { Method: RpcSystemCalls.reconnect, RelatedId: 9001 },
            [h.peer.ownHandshakeIndex, { 0: base64(IncreasingSeqCompressor.serialize([callId])) }],
            h.peer);
        await delay(20);

        const okForCall = `"RelatedId":${callId}`;
        expect(h.sent.some(
            f => f.includes(RpcSystemCalls.ok) && f.includes(okForCall))).toBe(true);
    });

    it('holds keep-alives while a re-accepted server peer is handshaking', async () => {
        const serverHub = newServerHub();
        const peer: RpcServerPeer = serverHub.getServerPeer('server://keepalive');
        peer.serializationFormat = format;
        peer.keepAlivePeriodMs = 20;

        // First generation: reach Connected, which arms the keep-alive timer.
        const [firstClientWs, firstServerWs] = createMockWsPair();
        peer.accept(new RpcWebSocketConnection(
            firstServerWs, format.isBinary, format, serverHub.registry));
        serverHub.systemCallSender.handshake(
            new RpcWebSocketConnection(firstClientWs, format.isBinary, format, serverHub.registry),
            format, 'client-peer', serverHub.hubId, 1);
        await delay(10);
        expect(peer.isConnected).toBe(true);

        // Second generation accepted while the old socket is still open, so no
        // `closed` event disarms the timer — it stays live across the handshake.
        const [, secondServerWs] = createMockWsPair();
        peer.accept(new RpcWebSocketConnection(
            secondServerWs, format.isBinary, format, serverHub.registry));
        expect(peer.isConnected).toBe(false);

        const sent: string[] = [];
        const origSend = secondServerWs.send.bind(secondServerWs);
        secondServerWs.send = data => {
            if (typeof data === 'string') sent.push(data);
            origSend(data);
        };
        await delay(60); // Several keep-alive periods.

        expect(sent.some(f => f.includes(RpcSystemCalls.keepAlive))).toBe(false);
    });
});
