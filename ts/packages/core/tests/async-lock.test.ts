import { describe, it, expect } from 'vitest';
import { AsyncLock } from '../src/index.js';

describe('AsyncLock', () => {
    it('should serialize access', async () => {
        const lock = new AsyncLock();
        const order: number[] = [];

        const task1 = lock.run(async () => {
            order.push(1);
            await new Promise(r => setTimeout(r, 10));
            order.push(2);
        });

        const task2 = lock.run(() => {
            order.push(3);
            order.push(4);
        });

        await Promise.all([task1, task2]);
        expect(order).toEqual([1, 2, 3, 4]);
    });

    it('should return the value from fn', async () => {
        const lock = new AsyncLock();
        const result = await lock.run(() => 42);
        expect(result).toBe(42);
    });

    it('should release lock on error', async () => {
        const lock = new AsyncLock();
        await expect(
            lock.run(() => {
                throw new Error('boom');
            })
        ).rejects.toThrow('boom');
        expect(lock.isLocked).toBe(false);
        // Should be able to acquire again
        const result = await lock.run(() => 'ok');
        expect(result).toBe('ok');
    });

    it('should throw when releasing an unlocked lock', () => {
        const lock = new AsyncLock();
        expect(() => lock.release()).toThrow();
    });

    it('should report isLocked correctly', async () => {
        const lock = new AsyncLock();
        expect(lock.isLocked).toBe(false);
        await lock.acquire();
        expect(lock.isLocked).toBe(true);
        lock.release();
        expect(lock.isLocked).toBe(false);
    });

    // C5: an already-aborted signal rejects acquire() with the reason.
    it('rejects acquire() immediately for an already-aborted signal', async () => {
        const lock = new AsyncLock();
        const ac = new AbortController();
        const reason = new Error('aborted');
        ac.abort(reason);
        await expect(lock.acquire(ac.signal)).rejects.toBe(reason);
        expect(lock.isLocked).toBe(false);
    });

    // C5: aborting a queued waiter rejects it and removes it from the queue,
    // without disturbing the holder or later waiters.
    it('aborts a queued waiter and lets the lock proceed normally afterwards', async () => {
        const lock = new AsyncLock();
        await lock.acquire(); // hold the lock

        const ac = new AbortController();
        const queued = lock.acquire(ac.signal);
        const reason = new Error('gave up waiting');
        ac.abort(reason);
        await expect(queued).rejects.toBe(reason);

        // The aborted waiter must not have grabbed the lock on release.
        lock.release();
        expect(lock.isLocked).toBe(false);

        const order: string[] = [];
        const survivor = lock.run(() => { order.push('survivor'); });
        await survivor;
        expect(order).toEqual(['survivor']);
    });

    it('does not reject a waiter whose signal aborts after it acquired', async () => {
        const lock = new AsyncLock();
        await lock.acquire();

        const ac = new AbortController();
        const acquired = lock.run(() => 'done', ac.signal);
        lock.release(); // hands the lock to the queued run()

        await expect(acquired).resolves.toBe('done');
        ac.abort(new Error('too late'));
        expect(lock.isLocked).toBe(false);
    });
});
