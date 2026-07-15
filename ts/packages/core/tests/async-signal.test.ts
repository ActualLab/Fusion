import { describe, it, expect } from 'vitest';
import { AsyncSignal } from '../src/index.js';

describe('AsyncSignal', () => {
    it('notify() releases a parked waiter', async () => {
        const signal = new AsyncSignal();
        let resolved = false;
        const w = signal.wait().then(() => { resolved = true; });
        expect(resolved).toBe(false);
        signal.notify();
        await w;
        expect(resolved).toBe(true);
    });

    it('one notify() releases every waiter parked at that moment', async () => {
        const signal = new AsyncSignal();
        const waits = [signal.wait(), signal.wait(), signal.wait()];
        signal.notify();
        await expect(Promise.all(waits)).resolves.toHaveLength(3);
    });

    it('is edge-triggered — a notify() with no waiter is not latched', async () => {
        const signal = new AsyncSignal();
        signal.notify();
        let resolved = false;
        void signal.wait().then(() => { resolved = true; });
        await new Promise(r => setTimeout(r, 20));
        expect(resolved).toBe(false);
    });

    it('auto-resets — waiters after a notify() need the next notify()', async () => {
        const signal = new AsyncSignal();
        signal.notify();
        let resolved = false;
        const w = signal.wait().then(() => { resolved = true; });
        await new Promise(r => setTimeout(r, 20));
        expect(resolved).toBe(false);
        signal.notify();
        await w;
        expect(resolved).toBe(true);
    });
});
