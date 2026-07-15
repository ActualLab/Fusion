import { awaitWithCleanup, type RetryDelaySeq } from '@actuallab/core';
import { uiActions } from './ui-action-tracker.js';
import {
    computeUpdateDelay,
    defaultMinDelayMs,
    defaultRetryDelays,
    type UpdateDelayer,
} from './update-delayer.js';

/** Re-computes after a delay, but short-circuits while UIActionTracker enables instant updates. */
export class UIUpdateDelayer {
    private static _cache = new Map<number, UpdateDelayer>();

    // Floors ms at minDelay like C# FixedDelayer.Get — a zero delayer requires FixedDelayer.zero.
    static get(ms: number): UpdateDelayer {
        ms = Math.max(ms, defaultMinDelayMs);
        let delayer = UIUpdateDelayer._cache.get(ms);
        if (delayer !== undefined) return delayer;

        delayer = new UIUpdateDelayer(ms).delay;
        UIUpdateDelayer._cache.set(ms, delayer);
        return delayer;
    }

    readonly ms: number;
    readonly retryDelays: RetryDelaySeq;
    readonly minDelay: number;
    readonly delay: UpdateDelayer;

    constructor(
        ms: number,
        retryDelays: RetryDelaySeq = defaultRetryDelays,
        minDelay: number = defaultMinDelayMs
    ) {
        this.ms = ms;
        this.retryDelays = retryDelays;
        this.minDelay = minDelay;
        this.delay = this._delay.bind(this);
    }

    // Private methods

    // Mirrors C# UpdateDelayer.Delay: race the instant-updates window against the full delay,
    // then enforce minDelay measured from delay start so instant updates never hot-loop the CPU.
    private async _delay(retryCount: number, abortSignal?: AbortSignal): Promise<void> {
        const t = uiActions;
        const minDelay = retryCount === 0 ? this.minDelay : this.retryDelays.min;
        const delayMs = computeUpdateDelay(retryCount, this.ms, this.retryDelays, this.minDelay);
        if (delayMs <= 0)
            return;

        const startedAt = Date.now();
        if (!t.areInstantUpdatesEnabled())
            await awaitWithCleanup(abortSignal, 'resolve', (complete, addCleanup) => {
                const timer = setTimeout(complete, delayMs);
                addCleanup(() => clearTimeout(timer));

                const onChanged = () => {
                    if (t.areInstantUpdatesEnabled())
                        complete();
                };
                t.changed.add(onChanged);
                addCleanup(() => t.changed.remove(onChanged));
            });
        if (abortSignal?.aborted)
            return;

        const remaining = minDelay - (Date.now() - startedAt);
        if (remaining > 0)
            await awaitWithCleanup(abortSignal, 'resolve', (complete, addCleanup) => {
                const timer = setTimeout(complete, remaining);
                addCleanup(() => clearTimeout(timer));
            });
    }
}
