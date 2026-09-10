// Regression tests for the pre-handshake send gate. `RpcStreamSender` used to
// write items through `peer.connection`, which `setupConnection` populates
// *before* the socket even opens (`rpc-peer.ts` sets it, then awaits
// `conn.whenConnected`) — and `RpcConnection` buffers pre-OPEN sends and flushes
// them from `onopen`. So the unsafe window is the whole Connecting phase plus
// Handshaking, not just the handshake round-trip, and an item produced during a
// reconnect hit the wire ahead of `$sys.Handshake`; the remote then rejected the
// connection with "Handshake failed: expected $sys.Handshake, got $sys.I".
// `isConnected` is the only flag that is false across both phases, which is why
// it — not `ws.readyState` — is the gate.
// .NET is immune because `RpcPeer.Transport` hides the transport until the
// handshake completes (RpcPeer.cs:30-53); `RpcStream._sendAck` already carried
// the equivalent guard, and the sender now matches it.
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
    RpcHub,
    RpcStreamSender,
    RpcSystemCalls,
    RpcSerializationFormat,
    RpcWebSocketConnection,
} from '../src/index.js';
import type { RpcServerPeer } from '../src/index.js';
import { createMockWsPair } from './mock-ws.js';
import { delay } from './rpc-test-helpers.js';

describe('RpcStreamSender pre-handshake send gate', () => {
    const format = RpcSerializationFormat.get('json5np');
    // No `RpcClientPeer` here on purpose: `connectWith` sends a handshake, which
    // is exactly the state this suite needs the server peer NOT to be in. The
    // client side is a bare connection we drive by hand.
    const clientPeerId = 'client-peer-id';
    let serverHub: RpcHub;
    let clientHub: RpcHub;
    let clientConn: RpcWebSocketConnection;
    let serverPeer: RpcServerPeer;
    let frames: string[];

    beforeEach(async () => {
        serverHub = new RpcHub('server-hub');
        clientHub = new RpcHub('client-hub');

        const [clientWs, serverWs] = createMockWsPair();
        clientConn = new RpcWebSocketConnection(clientWs, format.isBinary, format, clientHub.registry);
        const serverConn = new RpcWebSocketConnection(serverWs, format.isBinary, format, serverHub.registry);

        serverPeer = serverHub.getServerPeer('server://test');
        serverPeer.accept(serverConn);

        frames = [];
        const send = serverConn.send.bind(serverConn);
        serverConn.send = data => {
            frames.push(data);
            send(data);
        };

        await delay(5); // Let the mock sockets reach OPEN.
    });

    afterEach(() => {
        serverHub.close();
        clientHub.close();
    });

    function createSender(ackAdvance = 100): RpcStreamSender<number> {
        const sender = new RpcStreamSender<number>(serverPeer, 1, ackAdvance, true, false);
        serverPeer.sharedObjects.register(sender);
        return sender;
    }

    async function completeHandshake(): Promise<void> {
        clientHub.systemCallSender.handshake(
            clientConn, format, clientPeerId, clientHub.hubId, 1);
        await serverPeer.whenConnected();
    }

    function framesWith(method: string): string[] {
        return frames.filter(f => f.includes(method));
    }

    // The whole point of the gate: this state is reachable and looks "connected"
    // to anything that only checks `peer.connection`.
    it('accept() leaves the peer with a live connection but no handshake', () => {
        expect(serverPeer.connection).toBeDefined();
        expect(serverPeer.isConnected).toBe(false);
    });

    it('holds items and batches written before the handshake completes', () => {
        const sender = createSender();
        sender.sendItem(1);
        sender.sendBatch([2, 3]);

        expect(framesWith(RpcSystemCalls.item)).toHaveLength(0);
        expect(framesWith(RpcSystemCalls.batch)).toHaveLength(0);
        // Held items still advance the index, so replay-buffer indexing survives.
        expect(sender.nextIndex).toBe(3);
    });

    it('holds $sys.End written before the handshake completes', () => {
        const sender = createSender();
        sender.sendEnd();

        expect(framesWith(RpcSystemCalls.end)).toHaveLength(0);
    });

    it('holds $sys.Disconnect written before the handshake completes', () => {
        const sender = createSender();
        // A foreign hostId is the public path into `_sendDisconnect`.
        sender.onAck(0, '11111111-1111-1111-1111-111111111111');

        expect(framesWith(RpcSystemCalls.disconnect)).toHaveLength(0);
    });

    it('never emits an item ahead of the handshake on the wire', async () => {
        const sender = createSender();
        sender.sendItem(1);
        await completeHandshake();
        sender.sendItem(2);

        const itemAt = frames.findIndex(f => f.includes(RpcSystemCalls.item));
        const handshakeAt = frames.findIndex(f => f.includes(RpcSystemCalls.handshake));
        expect(handshakeAt).toBeGreaterThanOrEqual(0);
        expect(itemAt).toBeGreaterThan(handshakeAt);
    });

    it('sends normally once the handshake completes', async () => {
        await completeHandshake();
        const sender = createSender();
        sender.sendItem(1);
        sender.sendBatch([2, 3]);
        sender.sendEnd();

        expect(framesWith(RpcSystemCalls.item)).toHaveLength(1);
        expect(framesWith(RpcSystemCalls.batch)).toHaveLength(1);
        expect(framesWith(RpcSystemCalls.end)).toHaveLength(1);
    });

    it('replays items held during the handshake after the reset ack', async () => {
        // ackAdvance 3 makes the pump fill the window and go back to waiting
        // for an ACK, instead of parking inside the source read.
        const sender = createSender(3);
        sender.onAck(0, sender.id.hostId);
        // The source stays open past its 3 items so the pump can't end the
        // stream before the reset ack arrives.
        let release!: () => void;
        const blocked = new Promise<void>(r => { release = r; });
        const writeDone = sender.writeFrom((async function* () {
            yield 1;
            yield 2;
            yield 3;
            await blocked;
        })());
        await delay(10);

        expect(framesWith(RpcSystemCalls.item)).toHaveLength(0);
        expect(sender.nextIndex).toBe(3);

        await completeHandshake();
        sender.onAck(0, sender.id.hostId); // Reconnect ack — rewind to 0 and replay.
        await delay(10);

        expect(framesWith(RpcSystemCalls.item).length).toBeGreaterThanOrEqual(3);

        release();
        sender.disconnect();
        await writeDone;
    });

    // .NET has no "already ended" latch: the End is an ordinary buffer entry
    // (RpcSharedStream.cs:185/239) that a reset ack replays like any item, and
    // the pump keeps serving acks after it. TS used to set `_ended` inside
    // `sendEnd`, so an End dropped here was lost for good and the next ack got
    // $sys.Disconnect instead.
    it('replays an End held during the handshake after the reset ack', async () => {
        const sender = createSender(3);
        sender.onAck(0, sender.id.hostId);
        // eslint-disable-next-line @typescript-eslint/require-await
        const writeDone = sender.writeFrom((async function* () {
            yield 1;
        })());
        await delay(10);

        expect(framesWith(RpcSystemCalls.item)).toHaveLength(0);
        expect(framesWith(RpcSystemCalls.end)).toHaveLength(0);

        await completeHandshake();
        sender.onAck(0, sender.id.hostId);
        await delay(10);

        expect(framesWith(RpcSystemCalls.item).length).toBeGreaterThanOrEqual(1);
        expect(framesWith(RpcSystemCalls.end).length).toBeGreaterThanOrEqual(1);

        // The pump outlives the End, waiting for AckEnd (RpcSharedStream.cs:145).
        sender.onAckEnd('');
        await writeDone;
    });
});
