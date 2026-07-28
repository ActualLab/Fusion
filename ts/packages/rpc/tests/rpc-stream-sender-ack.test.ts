// Regression tests for review-r2 I7 — every accepted `$sys.Ack` used to be
// appended to a plain array with no validation, and the pump drained it with
// repeated `shift()`. A peer that owns a locally hosted stream could flood ACKs
// while the source was slow: the array grew without bound and draining it was
// quadratic. ACK state is now coalesced at receipt into one index plus one
// accumulated reset bit, and out-of-contract indexes are rejected.
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { RpcStreamSender } from '../src/index.js';
import { createTestHubPair, delay } from './rpc-test-helpers.js';
import type { TestHubPair } from './rpc-test-helpers.js';

interface AckState {
    _ackIndex: number;
    _ackMustReset: boolean;
    _nextIndex: number;
}

function ackState<T>(sender: RpcStreamSender<T>): AckState {
    return sender as unknown as AckState;
}

// eslint-disable-next-line @typescript-eslint/require-await
async function* counter(count: number): AsyncGenerator<number> {
    for (let i = 0; i < count; i++)
        yield i;
}

describe('RpcStreamSender ACK coalescing (I7)', () => {
    let pair: TestHubPair;

    beforeEach(() => {
        pair = createTestHubPair('json5np');
    });

    afterEach(() => {
        pair.serverHub.close();
        pair.clientHub.close();
    });

    function createSender(ackAdvance = 100, allowReconnect = false): RpcStreamSender<number> {
        const sender = new RpcStreamSender<number>(
            pair.serverPeer, 1, ackAdvance, allowReconnect, false);
        pair.serverPeer.sharedObjects.register(sender);
        return sender;
    }

    it('keeps ACK state at O(1) under a flood', async () => {
        const ackAdvance = 100;
        const sender = createSender(ackAdvance);
        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(counter(10_000));
        for (let i = 0; sender.nextIndex < ackAdvance && i < 100; i++)
            await delay(0);

        // 50k ACKs delivered without ever yielding to the pump. The old queue
        // would hold 50k entries here.
        for (let i = 1; i <= 50_000; i++)
            sender.onAck(i % (ackAdvance + 1), '');

        const state = ackState(sender);
        expect(state._ackIndex).toBe(ackAdvance);
        expect(typeof state._ackIndex).toBe('number');

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    it('accumulates the reset bit across coalesced ACKs', async () => {
        const sender = createSender(100, true);
        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(counter(1000));
        for (let i = 0; sender.nextIndex < 100 && i < 100; i++)
            await delay(0);

        sender.onAck(20, sender.id.hostId); // reset ACK
        sender.onAck(25, ''); // plain ACK right after it
        const state = ackState(sender);
        expect(state._ackIndex).toBe(25);
        expect(state._ackMustReset).toBe(true);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    it('drops a non-reset ACK that regresses below the last accepted index', async () => {
        const sender = createSender(100);
        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(counter(1000));
        for (let i = 0; sender.nextIndex < 100 && i < 100; i++)
            await delay(0);

        sender.onAck(40, '');
        sender.onAck(10, '');
        expect(ackState(sender)._ackIndex).toBe(40);
        expect(sender.abortSignal.aborted).toBe(false); // a regression is dropped, not fatal

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    it('still lets a reset ACK move the position backwards', async () => {
        const sender = createSender(100, true);
        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(counter(1000));
        for (let i = 0; sender.nextIndex < 100 && i < 100; i++)
            await delay(0);

        sender.onAck(40, '');
        sender.onAck(10, sender.id.hostId);
        const state = ackState(sender);
        expect(state._ackIndex).toBe(10);
        expect(state._ackMustReset).toBe(true);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    for (const [name, badIndex] of [
        ['NaN', Number.NaN],
        ['Infinity', Number.POSITIVE_INFINITY],
        ['a negative index', -1],
        ['a fractional index', 1.5],
        ['an unsafe integer', Number.MAX_SAFE_INTEGER + 2],
        ['an index past what was sent', 1_000_000],
    ] as [string, number][]) {
        it(`rejects ${name} instead of stalling the pump`, async () => {
            const ackAdvance = 100;
            const sender = createSender(ackAdvance);
            sender.onAck(0, sender.id.hostId);
            const writeDone = sender.writeFrom(counter(10_000));
            for (let i = 0; sender.nextIndex < ackAdvance && i < 100; i++)
                await delay(0);

            const sentBefore = sender.nextIndex;
            sender.onAck(badIndex, '');
            await delay(5);

            // The stream is terminated rather than left pumping into nowhere,
            // and `_nextIndex` never runs past what was actually sent.
            expect(sender.abortSignal.aborted).toBe(true);
            expect(ackState(sender)._nextIndex).toBe(sentBefore);

            await writeDone.catch(() => { /* noop */ });
        });
    }
});
