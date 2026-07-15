import { describe, it, expect } from 'vitest';
import { delayAsync, delayAsyncWith } from '../src/index.js';

describe('delayAsync', () => {
    it('resolves after roughly the requested delay', async () => {
        const started = Date.now();
        await delayAsync(20);
        const elapsed = Date.now() - started;
        // Generous lower bound (timer fairness on busy CI) and a sane upper bound.
        expect(elapsed).toBeGreaterThanOrEqual(15);
        expect(elapsed).toBeLessThan(500);
    });

    it('resolves with no value', async () => {
        // delayAsync returns Promise<void>; awaiting it should just complete.
        await delayAsync(0);
    });

    it('rejects immediately with the reason for an already-aborted signal', async () => {
        const ac = new AbortController();
        const reason = new Error('nope');
        ac.abort(reason);
        await expect(delayAsync(1000, ac.signal)).rejects.toBe(reason);
    });

    it('rejects with the reason on a live abort before the delay elapses', async () => {
        const ac = new AbortController();
        const promise = delayAsync(1000, ac.signal);
        const reason = new Error('aborted');
        ac.abort(reason);
        await expect(promise).rejects.toBe(reason);
    });

    it('resolves normally when the signal never aborts', async () => {
        const ac = new AbortController();
        await delayAsync(5, ac.signal);
    });
});

describe('delayAsyncWith', () => {
    it('resolves with the provided value', async () => {
        expect(await delayAsyncWith(0, 42)).toBe(42);
        expect(await delayAsyncWith(0, 'hi')).toBe('hi');
    });

    it('preserves reference identity', async () => {
        const obj = { id: 1 };
        expect(await delayAsyncWith(0, obj)).toBe(obj);
    });
});
