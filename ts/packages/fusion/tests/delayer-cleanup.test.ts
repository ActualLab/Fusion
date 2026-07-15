// Regression tests for docs/plans/ts-port-audit.md items S4 (guaranteed listener/handler
// cleanup in delayer waits) and S14 (delayers resolve, never reject, on abort).
import { describe, it, expect } from 'vitest';
import { FixedDelayer, UIUpdateDelayer, uiActions } from '../src/index.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

describe('Delayer wait cleanup (S4, S14)', () => {
    it('S14: FixedDelayer resolves (never rejects) when its abort signal fires', async () => {
        const ac = new AbortController();
        const p = FixedDelayer.get(20_000)(0, ac.signal); // long enough that only abort settles it
        ac.abort();

        await expect(p).resolves.toBeUndefined();
    });

    it('S14: FixedDelayer resolves when already aborted', async () => {
        const ac = new AbortController();
        ac.abort();

        await expect(FixedDelayer.get(20_000)(0, ac.signal)).resolves.toBeUndefined();
    });

    it('S4: UIUpdateDelayer removes its changed handler on the normal timer path', async () => {
        const before = uiActions.changed.count;
        await UIUpdateDelayer.get(10)(0);

        expect(uiActions.changed.count).toBe(before);
    });

    it('S4: UIUpdateDelayer removes its changed handler on the abort path', async () => {
        const before = uiActions.changed.count;
        const ac = new AbortController();
        const p = UIUpdateDelayer.get(20_000)(0, ac.signal);
        ac.abort();
        await p;

        expect(uiActions.changed.count).toBe(before);
    });

    it('S14: an aborted delayer wait raises no unhandledRejection', async () => {
        const rejections: unknown[] = [];
        const onUnhandled = (reason: unknown) => rejections.push(reason);
        process.on('unhandledRejection', onUnhandled);
        try {
            const ac = new AbortController();
            // Orphan the wait (no awaiter/handler), then abort — the pre-fix
            // >10 s branch rejected here and crashed Node with ERR_UNHANDLED_REJECTION.
            void FixedDelayer.get(20_000)(0, ac.signal);
            void UIUpdateDelayer.get(20_000)(0, ac.signal);
            ac.abort();
            await delay(20);
            await new Promise(r => setImmediate(r));
        } finally {
            process.off('unhandledRejection', onUnhandled);
        }
        expect(rejections).toEqual([]);
    });
});
