import { describe, it, expect } from 'vitest';
import { serialize } from '../src/index.js';

interface Deferred<T> {
    promise: Promise<T>;
    resolve: (v: T) => void;
    reject: (e: unknown) => void;
}

function deferred<T>(): Deferred<T> {
    let resolve!: (v: T) => void;
    let reject!: (e: unknown) => void;
    const promise = new Promise<T>((res, rej) => { resolve = res; reject = rej; });
    return { promise, resolve, reject };
}

describe('serialize', () => {
    it('runs concurrent invocations sequentially', async () => {
        const events: string[] = [];
        const gates = [deferred<undefined>(), deferred<undefined>(), deferred<undefined>()];
        let i = 0;
        const fn = serialize(async (label: string) => {
            events.push(`start:${label}`);
            await gates[i++].promise;
            events.push(`end:${label}`);
            return label;
        });

        const p1 = fn('a');
        const p2 = fn('b');
        const p3 = fn('c');

        // settleMicrotasks: drain enough microtask ticks for any pending
        // serializer plumbing to advance to its next blocking await.
        const settle = async (): Promise<void> => {
            for (let k = 0; k < 8; k++) await Promise.resolve();
        };

        await settle();
        expect(events).toEqual(['start:a']);

        gates[0].resolve();
        await p1;
        await settle();
        expect(events).toEqual(['start:a', 'end:a', 'start:b']);

        gates[1].resolve();
        await p2;
        await settle();
        expect(events).toEqual(['start:a', 'end:a', 'start:b', 'end:b', 'start:c']);

        gates[2].resolve();
        expect(await p3).toBe('c');
        expect(events).toEqual(['start:a', 'end:a', 'start:b', 'end:b', 'start:c', 'end:c']);
    });

    it('forwards the function result back to each caller', async () => {
        const fn = serialize((n: number) => n * 2);
        const results = await Promise.all([fn(1), fn(2), fn(3)]);
        expect(results).toEqual([2, 4, 6]);
    });

    it('coalesces calls past `limit` onto the last in-flight call', async () => {
        const gates = [deferred<undefined>(), deferred<undefined>()];
        let i = 0;
        const fn = serialize(async (label: string) => {
            await gates[Math.min(i++, gates.length - 1)].promise;
            return label;
        }, /* limit */ 2);

        const p1 = fn('a');
        const p2 = fn('b');
        const p3 = fn('c');  // queue full → coalesced onto p2
        const p4 = fn('d');  // also coalesced

        gates[0].resolve();
        gates[1].resolve();

        expect(await p1).toBe('a');
        expect(await p2).toBe('b');
        expect(await p3).toBe('b'); // same value as p2
        expect(await p4).toBe('b');
    });

    it('does not let a prior rejection poison the queue', async () => {
        let attempts = 0;
        const fn = serialize((shouldFail: boolean) => {
            attempts++;
            if (shouldFail) throw new Error('boom');
            return attempts;
        });

        const p1 = fn(true);   // fails
        const p2 = fn(false);  // must run independently
        const p3 = fn(false);  // and so must this one

        await expect(p1).rejects.toThrow('boom');
        expect(await p2).toBe(2);
        expect(await p3).toBe(3);
    });

    it('preserves serial order even when an intermediate call rejects', async () => {
        const order: string[] = [];
        const fn = serialize((label: string, fail: boolean) => {
            order.push(label);
            if (fail) throw new Error(label);
        });

        const p1 = fn('a', false);
        const p2 = fn('b', true);
        const p3 = fn('c', false);

        await p1;
        await expect(p2).rejects.toThrow('b');
        await p3;
        expect(order).toEqual(['a', 'b', 'c']);
    });
});
