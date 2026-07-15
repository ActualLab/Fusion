import { awaitWithCleanup } from '@actuallab/core';
import { uiActions } from './ui-action-tracker.js';
import { FixedDelayer, type UpdateDelayer } from './update-delayer.js';

/** Re-computes after a fixed delay, but skips the delay when UIActionTracker is active. */
export class UIUpdateDelayer {
    private static _cache = new Map<number, UpdateDelayer>();

    static get(ms: number): UpdateDelayer {
        if (ms <= 0) return FixedDelayer.zero;
        let delayer = UIUpdateDelayer._cache.get(ms);
        if (delayer !== undefined) return delayer;

        const t = uiActions;
        delayer = (abortSignal?: AbortSignal) => {
            if (t.isActive) return Promise.resolve();

            return awaitWithCleanup(abortSignal, 'resolve', (complete, addCleanup) => {
                const timer = setTimeout(complete, ms);
                addCleanup(() => clearTimeout(timer));

                // Cancel the delay when the tracker becomes active
                const onChanged = () => {
                    if (t.isActive) complete();
                };
                t.changed.add(onChanged);
                addCleanup(() => t.changed.remove(onChanged));
            });
        };
        UIUpdateDelayer._cache.set(ms, delayer);
        return delayer;
    }
}
