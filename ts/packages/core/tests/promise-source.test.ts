import { describe, it, expect } from 'vitest';
import { PromiseSource } from '../src/index.js';

describe('PromiseSource', () => {
    it('should resolve with value', async () => {
        const ps = new PromiseSource<number>();
        expect(ps.isCompleted).toBe(false);
        ps.resolve(42);
        expect(ps.isCompleted).toBe(true);
        expect(await ps.promise).toBe(42);
    });

    it('should reject with error', async () => {
        const ps = new PromiseSource<number>();
        ps.reject(new Error('fail'));
        expect(ps.isCompleted).toBe(true);
        await expect(ps.promise).rejects.toThrow('fail');
    });

    it('should return false on duplicate resolve', () => {
        const ps = new PromiseSource<number>();
        expect(ps.resolve(1)).toBe(true);
        expect(ps.resolve(2)).toBe(false);
    });

    it('should return false on resolve after reject', async () => {
        const ps = new PromiseSource<number>();
        expect(ps.reject(new Error('fail'))).toBe(true);
        expect(ps.resolve(42)).toBe(false);
        // Consume the rejection to avoid unhandled rejection warning
        await expect(ps.promise).rejects.toThrow('fail');
    });

    it('is awaitable directly (Promise<T>)', async () => {
        const ps = new PromiseSource<number>();
        ps.resolve(42);
        expect(await ps).toBe(42);
    });

    it('then() delegates to the inner promise on fulfilment', async () => {
        const ps = new PromiseSource<number>();
        ps.resolve(7);
        const doubled = await ps.then(v => v * 2);
        expect(doubled).toBe(14);
    });

    it('catch() delegates to the inner promise on rejection', async () => {
        const ps = new PromiseSource<number>();
        ps.reject(new Error('boom'));
        const recovered = await ps.catch((e: unknown) => (e as Error).message);
        expect(recovered).toBe('boom');
    });

    it('finally() runs after settlement', async () => {
        const ps = new PromiseSource<number>();
        let ran = false;
        const settled = ps.finally(() => { ran = true; });
        ps.resolve(1);
        expect(await settled).toBe(1);
        expect(ran).toBe(true);
    });

    it('exposes Symbol.toStringTag === "Promise"', () => {
        const ps = new PromiseSource<number>();
        expect(ps[Symbol.toStringTag]).toBe('Promise');
    });

    it('.promise is still accessible and equals the awaited value', async () => {
        const ps = new PromiseSource<number>();
        ps.resolve(99);
        expect(await ps.promise).toBe(99);
        expect(await ps).toBe(await ps.promise);
    });
});
