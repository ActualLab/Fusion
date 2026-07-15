// Regression tests for docs/plans/ts-port-audit.md batch "stateui":
//   S8  — UpdateDelayer (retryCount, abortSignal); RetryDelaySeq backoff, reset on success.
//   S9  — UIActionTracker instant-update window; UIUpdateDelayer consults it.
//   S10 — UIUpdateDelayer enforces minDelay even on the instant/short-circuit path.
//   S17 — useMutableState returns { value, error, set } (validated via MutableState's shape,
//         since fusion-react has no React test infra — react is not installed).
//   S18 — UIActionTracker.errors recency dedup + size cap.
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { AsyncContext, RetryDelaySeq, errorResult } from '@actuallab/core';
import {
    ComputedState,
    FixedDelayer,
    MutableState,
    UIActionTracker,
    UIUpdateDelayer,
    uiActions,
    type UpdateDelayer,
} from '../src/index.js';

function flush(): Promise<void> {
    return new Promise(r => setTimeout(r, 0));
}

describe('S8 — UpdateDelayer retry backoff', () => {
    afterEach(() => {
        vi.useRealTimers();
    });

    async function assertWaits(
        delayer: UpdateDelayer,
        retryCount: number,
        expectedMs: number
    ): Promise<void> {
        let done = false;
        const p = delayer(retryCount).then(() => void (done = true));
        await vi.advanceTimersByTimeAsync(expectedMs - 1);
        expect(done).toBe(false);
        await vi.advanceTimersByTimeAsync(1);
        await p;
        expect(done).toBe(true);
    }

    it('grows the delay via RetryDelaySeq for retryCount > 0', async () => {
        vi.useFakeTimers();
        // spread 0 for deterministic timing; 1 s -> 1 min like C# RetryDelays.
        const retry = RetryDelaySeq.exp(1000, 60_000, 0);
        const delayer = new FixedDelayer(500, retry).delay;

        await assertWaits(delayer, 0, 500); // retryCount 0 -> the update delay (>= minDelay)
        await assertWaits(delayer, 1, 1000); // RetryDelays[1] = min
        await assertWaits(delayer, 2, Math.round(1000 * Math.SQRT2)); // RetryDelays[2] = min * sqrt(2)
    });

    it('retryCount 0 is floored by minDelay (32 ms)', async () => {
        vi.useFakeTimers();
        const delayer = new FixedDelayer(10).delay; // below the 32 ms floor
        await assertWaits(delayer, 0, 32);
    });

    it('get(0) is floored at minDelay — no silent zero-delay escape (C# FixedDelayer.Get parity)', async () => {
        vi.useFakeTimers();
        await assertWaits(FixedDelayer.get(0), 0, 32);
        await assertWaits(UIUpdateDelayer.get(0), 0, 32);
    });

    it('ComputedState grows retryCount on consecutive errors and resets on success', async () => {
        AsyncContext.current = undefined;
        const seen: number[] = [];
        const spy: UpdateDelayer = retryCount => {
            seen.push(retryCount);
            return Promise.resolve();
        };
        let mode: 'error' | 'ok' = 'error';
        const state = new ComputedState<number>(
            () => {
                if (mode === 'error') throw new Error('x');
                return 1;
            },
            { updateDelayer: spy }
        );

        // iter1 computed an error synchronously; the cycle now awaits whenInvalidated.
        await flush();
        expect(seen).toEqual([]);

        state.computed.invalidate(); // -> delay(retryCount=1), iter2 (error)
        await flush();
        expect(seen).toEqual([1]);

        mode = 'ok';
        state.computed.invalidate(); // -> delay(retryCount=2), iter3 (ok) resets to 0
        await flush();
        expect(seen).toEqual([1, 2]);

        state.computed.invalidate(); // -> delay(retryCount=0)
        await flush();
        expect(seen).toEqual([1, 2, 0]);

        state.dispose();
    });
});

describe('S9 / S10 — instant-update window + minDelay floor', () => {
    afterEach(() => {
        vi.useRealTimers();
    });

    it('S9: an invalidation within the instant window updates promptly; outside it waits the full delay', async () => {
        vi.useFakeTimers();
        const delayer = UIUpdateDelayer.get(5000);

        // A just-completed UI action opens the ~300 ms instant-update window.
        await uiActions.run(() => Promise.resolve());

        let inside = false;
        const pInside = delayer(0).then(() => void (inside = true));
        await vi.advanceTimersByTimeAsync(31);
        expect(inside).toBe(false); // still held by the minDelay floor
        await vi.advanceTimersByTimeAsync(1);
        await pInside;
        expect(inside).toBe(true); // resolved at minDelay (32 ms), not 5000 ms

        // Close the window, then a fresh wait must run the full delay.
        await vi.advanceTimersByTimeAsync(400);
        let outside = false;
        const pOutside = delayer(0).then(() => void (outside = true));
        await vi.advanceTimersByTimeAsync(4999);
        expect(outside).toBe(false);
        await vi.advanceTimersByTimeAsync(1);
        await pOutside;
        expect(outside).toBe(true);
    });

    it('S10: the instant short-circuit still waits minDelay (never a zero-delay hot loop)', async () => {
        vi.useFakeTimers();
        const delayer = UIUpdateDelayer.get(5000);

        // Keep a UI action active so instant updates are enabled for the whole wait.
        let release!: () => void;
        const gate = new Promise<void>(r => {
            release = r;
        });
        const actionP = uiActions.run(() => gate);

        let done = false;
        const p = delayer(0).then(() => void (done = true));
        await vi.advanceTimersByTimeAsync(31);
        expect(done).toBe(false); // not zero — minDelay is enforced
        await vi.advanceTimersByTimeAsync(1);
        await p;
        expect(done).toBe(true);

        release();
        await actionP;
    });
});

describe('S17 — useMutableState error shape', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('a MutableState holding an error yields value=undefined + error (no throw)', () => {
        // The hook returns { value: state.valueOrUndefined, error: state.error, set }.
        // fusion-react has no React test infra (react is not installed), so we assert the
        // underlying MutableState contract the hook relies on.
        const boom = new Error('boom');
        const s = new MutableState<number>(errorResult(boom));

        expect(s.valueOrUndefined).toBeUndefined();
        expect(s.error).toBe(boom);
        expect(() => s.value).toThrow('boom'); // the old hook returned state.value -> threw on render
    });
});

describe('S18 — UIActionTracker.errors dedup + cap', () => {
    afterEach(() => {
        vi.useRealTimers();
    });

    it('dedups a same-name/same-message error within the recency window, re-adds after it', async () => {
        vi.useFakeTimers();
        const t = new UIActionTracker();

        await t.run(() => Promise.reject(new Error('same')));
        await t.run(() => Promise.reject(new Error('same')));
        expect(t.errors.length).toBe(1); // duplicate within recency dropped

        await t.run(() => Promise.reject(new TypeError('same')));
        expect(t.errors.length).toBe(2); // different name -> kept

        await vi.advanceTimersByTimeAsync(1100); // beyond maxDuplicateRecency (1 s)
        await t.run(() => Promise.reject(new Error('same')));
        expect(t.errors.length).toBe(3); // recency expired -> re-added
    });

    it('caps the error list as a backstop', async () => {
        const t = new UIActionTracker();
        t.maxErrors = 5;
        for (let i = 0; i < 20; i++)
            await t.run(() => Promise.reject(new Error(`e${i}`))); // all distinct -> never deduped

        expect(t.errors.length).toBe(5);
    });
});
