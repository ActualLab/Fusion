import { describe, it, expect } from 'vitest';
import { throttle, debounce } from '../src/index.js';

function sleep(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

describe('throttle', () => {
    it('default: fires head call immediately, then collapses tail calls', async () => {
        const calls: number[] = [];
        const fn = throttle((n: number) => { calls.push(n); }, 30);

        fn(1); // head, fires now
        fn(2); // dropped
        fn(3); // becomes the tail
        expect(calls).toEqual([1]);

        await sleep(60);
        expect(calls).toEqual([1, 3]);
    });

    it('skip: drops every call after the head until the interval ends', async () => {
        const calls: number[] = [];
        const fn = throttle((n: number) => { calls.push(n); }, 30, 'skip');

        fn(1);
        fn(2);
        fn(3);
        await sleep(60);
        expect(calls).toEqual([1]);
    });

    it('delayHead: defers even the head call by one interval', async () => {
        const calls: number[] = [];
        const fn = throttle((n: number) => { calls.push(n); }, 30, 'delayHead');

        fn(1);
        fn(2);
        fn(3);
        expect(calls).toEqual([]); // nothing yet
        await sleep(60);
        expect(calls).toEqual([3]); // only the latest fires
    });

    it('reset() discards a pending tail call', async () => {
        const calls: number[] = [];
        const fn = throttle((n: number) => { calls.push(n); }, 30);

        fn(1);
        fn(2);
        fn.reset();
        await sleep(60);
        expect(calls).toEqual([1]); // tail discarded
    });

    it('survives an inner throw without breaking subsequent calls', async () => {
        let calls = 0;
        const fn = throttle(() => {
            calls++;
            if (calls === 1) throw new Error('boom');
        }, 10);

        fn();
        await sleep(30);
        fn();
        await sleep(30);
        expect(calls).toBe(2);
    });
});

describe('debounce', () => {
    it('fires only after the trailing edge of a burst', async () => {
        const calls: number[] = [];
        const fn = debounce((n: number) => { calls.push(n); }, 20);

        fn(1);
        await sleep(5);
        fn(2);
        await sleep(5);
        fn(3);
        expect(calls).toEqual([]);
        await sleep(60);
        expect(calls).toEqual([3]);
    });

    it('reset() prevents a pending fire', async () => {
        const calls: number[] = [];
        const fn = debounce(() => { calls.push(1); }, 20);

        fn();
        fn.reset();
        await sleep(40);
        expect(calls).toEqual([]);
    });

    it('reuses an existing timer instead of re-arming repeatedly', async () => {
        const calls: number[] = [];
        const fn = debounce((n: number) => { calls.push(n); }, 30);

        // Hammer it; final value should win once quiet for 30ms.
        for (let i = 0; i < 50; i++) fn(i);
        await sleep(60);
        expect(calls).toEqual([49]);
    });
});
