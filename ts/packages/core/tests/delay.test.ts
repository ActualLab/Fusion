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
