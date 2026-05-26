import { describe, it, expect } from 'vitest';
import { abortPromise } from '../src/index.js';

describe('abortPromise', () => {
    it('rejects when the signal fires', async () => {
        const ac = new AbortController();
        const p = abortPromise(ac.signal);
        ac.abort(new Error('stop'));
        await expect(p).rejects.toThrow('stop');
    });

    it('rejects immediately for an already-aborted signal', async () => {
        const ac = new AbortController();
        ac.abort(new Error('already gone'));
        await expect(abortPromise(ac.signal)).rejects.toThrow('already gone');
    });

    it('forwards the signal.reason as-is (DOMException for default abort)', async () => {
        const ac = new AbortController();
        const p = abortPromise(ac.signal);
        ac.abort(); // default reason: DOMException
        await expect(p).rejects.toMatchObject({ name: 'AbortError' });
    });

    it('returns the same promise on repeated calls (no listener accumulation)', () => {
        const ac = new AbortController();
        const p1 = abortPromise(ac.signal);
        const p2 = abortPromise(ac.signal);
        const p3 = abortPromise(ac.signal);
        expect(p1).toBe(p2);
        expect(p2).toBe(p3);
    });

    it('races correctly with a fast upstream', async () => {
        const ac = new AbortController();
        const upstream = new Promise<string>(resolve => setTimeout(() => resolve('done'), 5));
        const winner = await Promise.race([upstream, abortPromise(ac.signal).catch(() => 'aborted')]);
        expect(winner).toBe('done');
    });

    it('races correctly with abort beating upstream', async () => {
        const ac = new AbortController();
        const upstream = new Promise<string>(resolve => setTimeout(() => resolve('done'), 50));
        setTimeout(() => ac.abort(new Error('stop')), 5);
        const winner = await Promise.race([upstream, abortPromise(ac.signal).catch(() => 'aborted')]);
        expect(winner).toBe('aborted');
    });
});
