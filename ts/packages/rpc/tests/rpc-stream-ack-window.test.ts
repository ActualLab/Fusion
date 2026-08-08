// Regression tests for review-r2 I6 — the remote `RpcStream` receiver used to
// accept and buffer every in-order item or batch into an uncapped `Denque`,
// never enforcing the `ackAdvance` window it advertised. A compromised or buggy
// server could flood `$sys.I`/`$sys.B` until the tab died.
//
// Per the maintainer's decision on B3/I6 the receive buffer is deliberately NOT
// bounded (a blocking/failing write sits on the peer's inbound message path and
// would head-of-line block every call on that peer). Instead the receiver
// enforces the advertised window and fails the stream on a violation.
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
    RpcHub,
    RpcClientPeer,
    RpcStream,
    createMessageChannelPair,
    parseStreamRef,
} from '../src/index.js';
import type { RpcServerPeer } from '../src/index.js';
import { delay } from './rpc-test-helpers.js';

const ACK_PERIOD = 10;
const ACK_ADVANCE = 20;
const MAX_BATCH_SIZE = 1024;
// What a conforming sender can leave buffered: ackAdvance in steady state, plus another
// ackAdvance after a reset re-bases it to what we've received, plus the ack period acks lag by.
const WINDOW = 2 * ACK_ADVANCE + ACK_PERIOD;

describe('RpcStream ack-window enforcement (I6)', () => {
    let serverHub: RpcHub;
    let clientHub: RpcHub;
    let clientPeer: RpcClientPeer;
    let serverPeer: RpcServerPeer;
    let ackEndCount: number;

    beforeEach(async () => {
        serverHub = new RpcHub('server-hub');
        clientHub = new RpcHub('client-hub');
        const [cc, sc] = createMessageChannelPair();
        clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(cc);
        clientHub.addPeer(clientPeer);
        serverPeer = serverHub.getServerPeer('server://test');
        serverPeer.accept(sc);
        await delay(10);

        ackEndCount = 0;
        const sender = clientHub.systemCallSender;
        const origAckEnd = sender.ackEnd.bind(sender);
        sender.ackEnd = (...args: Parameters<typeof origAckEnd>) => {
            ackEndCount++;
            origAckEnd(...args);
        };
    });

    afterEach(() => {
        serverHub.close();
        clientHub.close();
    });

    function createRemoteStream(
        localId = 1,
        ackPeriod = ACK_PERIOD,
        ackAdvance = ACK_ADVANCE,
    ): RpcStream<number> {
        const ref = parseStreamRef(`h,${localId},${ackPeriod},${ackAdvance},1,0`)!;
        const stream = new RpcStream<number>(ref, clientPeer);
        clientPeer.remoteObjects.register(stream);
        return stream;
    }

    function buffered(stream: RpcStream<number>): number {
        return (stream as unknown as { _buffer: { length: number } })._buffer.length;
    }

    it('accepts exactly the advertised window and fails on the item past it', async () => {
        const stream = createRemoteStream();
        for (let i = 0; i < WINDOW; i++)
            stream.onItem(i, i);

        expect(clientPeer.remoteObjects.get(1)).toBe(stream);
        expect(ackEndCount).toBe(0);

        stream.onItem(WINDOW, WINDOW);

        // Failing the stream unregisters it and acknowledges the end, so the
        // remote's shared stream is released instead of pumping into nowhere.
        expect(clientPeer.remoteObjects.get(1)).toBeUndefined();
        expect(ackEndCount).toBe(1);
        expect(buffered(stream)).toBe(WINDOW);

        await expect(drain(stream)).rejects.toThrow('ran past its ack window');
    });

    it('does not widen the window when a forced gap makes us send a reset ack', async () => {
        const stream = createRemoteStream();

        // An out-of-order index makes us send a reset ack, which re-bases the sender to what
        // we've received. Crediting our own window from that ack would let a flooding peer
        // ratchet its limit up one forced gap at a time.
        let index = 0;
        for (let round = 0; round < 50; round++) {
            for (let i = 0; i < ACK_ADVANCE; i++)
                stream.onItem(index++, index);

            stream.onItem(index + 1000, -1);
        }

        expect(buffered(stream)).toBeLessThanOrEqual(WINDOW);
        await expect(drain(stream)).rejects.toThrow('ran past its ack window');
    });

    it('rejects a batch larger than the maximum a sender may emit', async () => {
        // The window is wide enough to admit this batch, so only the size cap can reject it
        const stream = createRemoteStream(1, 100_000, 1_000_000);
        stream.onBatch(0, [...Array(MAX_BATCH_SIZE + 1).keys()]);

        expect(clientPeer.remoteObjects.get(1)).toBeUndefined();
        expect(buffered(stream)).toBe(0);

        await expect(drain(stream)).rejects.toThrow('batch is too large');
    });

    it('accepts a batch of exactly the maximum size', async () => {
        const stream = createRemoteStream(1, 100_000, 1_000_000);
        stream.onBatch(0, [...Array(MAX_BATCH_SIZE).keys()]);

        expect(clientPeer.remoteObjects.get(1)).toBe(stream);
        expect(buffered(stream)).toBe(MAX_BATCH_SIZE);
    });

    it('re-opens the window as the consumer acknowledges', async () => {
        const stream = createRemoteStream();
        for (let i = 0; i < ACK_ADVANCE; i++)
            stream.onItem(i, i);

        const consumed: number[] = [];
        const iterator = stream[Symbol.asyncIterator]();
        for (let i = 0; i < ACK_ADVANCE; i++) {
            const next = await iterator.next();
            consumed.push(next.value as number);
        }
        expect(consumed).toEqual([...Array(ACK_ADVANCE).keys()]);

        // Consumption sent acks, so the remote may run further ahead now.
        for (let i = ACK_ADVANCE; i < ACK_ADVANCE + ACK_PERIOD; i++)
            stream.onItem(i, i);
        expect(clientPeer.remoteObjects.get(1)).toBe(stream);
        expect((await iterator.next()).value).toBe(ACK_ADVANCE);
    });

    it('fails on a batch that lands past the window', async () => {
        const stream = createRemoteStream();
        stream.onBatch(0, [...Array(WINDOW + 1).keys()]);

        expect(clientPeer.remoteObjects.get(1)).toBeUndefined();
        expect(ackEndCount).toBe(1);
        expect(buffered(stream)).toBe(0);

        await expect(drain(stream)).rejects.toThrow('ran past its ack window');
    });

    it('keeps the buffer bounded under a flood', async () => {
        const stream = createRemoteStream();
        for (let i = 0; i < 10_000; i++)
            stream.onItem(i, i);

        expect(buffered(stream)).toBe(WINDOW);
        await expect(drain(stream)).rejects.toThrow('ran past its ack window');
    });

    for (const [name, badIndex] of [
        ['NaN', Number.NaN],
        ['Infinity', Number.POSITIVE_INFINITY],
        ['a negative index', -1],
        ['a fractional index', 2.5],
        ['an unsafe integer', Number.MAX_SAFE_INTEGER + 2],
    ] as [string, number][]) {
        it(`rejects ${name} in $sys.I`, async () => {
            const stream = createRemoteStream();
            stream.onItem(badIndex, 0);

            expect(clientPeer.remoteObjects.get(1)).toBeUndefined();
            await expect(drain(stream)).rejects.toThrow('invalid $sys.I index');
        });
    }

    it('rejects a non-integer index in $sys.B and $sys.End', async () => {
        const batchStream = createRemoteStream(1);
        batchStream.onBatch(Number.NaN, [1, 2]);
        await expect(drain(batchStream)).rejects.toThrow('invalid $sys.B index');

        const endStream = createRemoteStream(2);
        endStream.onEnd(-1, null);
        await expect(drain(endStream)).rejects.toThrow('invalid $sys.End index');
    });
});

async function drain(stream: RpcStream<number>): Promise<number[]> {
    const items: number[] = [];
    for await (const item of stream)
        items.push(item);
    return items;
}
