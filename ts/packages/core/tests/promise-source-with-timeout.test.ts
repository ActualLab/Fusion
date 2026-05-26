import { describe, it, expect } from 'vitest';
import { PromiseSourceWithTimeout, TimeoutError } from '../src/index.js';

describe('PromiseSourceWithTimeout', () => {
    it('rejects with TimeoutError when the timeout fires without a custom callback', async () => {
        const ps = new PromiseSourceWithTimeout<string>();
        ps.setTimeout(10);
        await expect(ps.promise).rejects.toBeInstanceOf(TimeoutError);
        expect(ps.hasTimeout()).toBe(false); // cleared after firing
    });

    it('runs a custom callback when provided', async () => {
        const ps = new PromiseSourceWithTimeout<string>();
        ps.setTimeout(10, () => ps.resolve('fallback'));
        expect(await ps.promise).toBe('fallback');
    });

    it('auto-clears the timer on resolve', async () => {
        const ps = new PromiseSourceWithTimeout<number>();
        ps.setTimeout(10);
        expect(ps.hasTimeout()).toBe(true);
        ps.resolve(7);
        expect(ps.hasTimeout()).toBe(false);
        expect(await ps.promise).toBe(7);
    });

    it('auto-clears the timer on reject', async () => {
        const ps = new PromiseSourceWithTimeout<number>();
        ps.setTimeout(10);
        ps.reject(new Error('boom'));
        expect(ps.hasTimeout()).toBe(false);
        await expect(ps.promise).rejects.toThrow('boom');
    });

    it('clearTimeout() cancels a pending timer', async () => {
        const ps = new PromiseSourceWithTimeout<number>();
        ps.setTimeout(5);
        ps.clearTimeout();
        // Wait long enough for the timer to have fired if not cancelled
        await new Promise(r => setTimeout(r, 20));
        // Still pending — resolve manually
        expect(ps.isCompleted).toBe(false);
        ps.resolve(1);
        expect(await ps.promise).toBe(1);
    });

    it('setTimeout(null) is a no-op on a settled promise', () => {
        const ps = new PromiseSourceWithTimeout<number>();
        ps.resolve(1);
        ps.setTimeout(10);
        expect(ps.hasTimeout()).toBe(false); // never installs after completion
    });

    it('replacing an existing timer cancels the old one', async () => {
        const ps = new PromiseSourceWithTimeout<string>();
        let firstFired = false;
        ps.setTimeout(5, () => { firstFired = true; });
        ps.setTimeout(20, () => ps.resolve('second'));
        expect(await ps.promise).toBe('second');
        expect(firstFired).toBe(false);
    });

    it('inherits Promise<T> surface from PromiseSource (awaitable directly)', async () => {
        const ps = new PromiseSourceWithTimeout<number>();
        ps.resolve(5);
        expect(await ps).toBe(5);
        expect(ps[Symbol.toStringTag]).toBe('Promise');
    });
});
