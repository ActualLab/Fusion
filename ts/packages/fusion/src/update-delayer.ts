import { awaitWithCleanup, RetryDelaySeq } from '@actuallab/core';

/** Controls when state re-computation happens after invalidation, per consecutive-error retryCount. */
export type UpdateDelayer = (
    retryCount: number,
    abortSignal?: AbortSignal
) => Promise<void>;

/** 1 s → 1 min exponential backoff — C# FixedDelayer.Defaults.RetryDelays parity. */
export const defaultRetryDelays = RetryDelaySeq.exp(1000, 60_000);

/** Windows timer period is 15.6 ms, so 32 ms = 2..3 timer ticks — C# FixedDelayer.Defaults.MinDelay. */
export const defaultMinDelayMs = 32;

// C# UpdateDelayer.GetDelay + minDelay floor: retryCount 0 uses the update delay
// floored by minDelay; retryCount > 0 uses RetryDelays[retryCount] floored by RetryDelays.Min.
export function computeUpdateDelay(
    retryCount: number,
    updateDelayMs: number,
    retryDelays: RetryDelaySeq,
    minDelayMs: number
): number {
    const min = retryCount === 0 ? minDelayMs : retryDelays.min;
    const raw = retryCount > 0 ? retryDelays.getDelay(retryCount) : updateDelayMs;
    return Math.max(min, raw);
}

/** Re-computes after a fixed update delay, with RetryDelaySeq backoff for consecutive errors. */
export class FixedDelayer {
    static readonly zero: UpdateDelayer = () => Promise.resolve();

    private static _cache = new Map<number, UpdateDelayer>();

    // Floors ms at minDelay like C# FixedDelayer.Get — a zero delayer requires the explicit .zero.
    static get(ms: number): UpdateDelayer {
        ms = Math.max(ms, defaultMinDelayMs);
        let delayer = FixedDelayer._cache.get(ms);
        if (delayer === undefined) {
            delayer = new FixedDelayer(ms).delay;
            FixedDelayer._cache.set(ms, delayer);
        }
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
        // Abort stops the wait early (resolves, never rejects) so a disposed
        // ComputedState's update loop terminates promptly and the race loser
        // carries no unhandled rejection (C# SuppressCancellationAwait spirit).
        this.delay = (retryCount, abortSignal) => {
            const delayMs = computeUpdateDelay(
                retryCount,
                this.ms,
                this.retryDelays,
                this.minDelay
            );
            if (delayMs <= 0) return Promise.resolve();

            return awaitWithCleanup(abortSignal, 'resolve', (complete, addCleanup) => {
                const timer = setTimeout(complete, delayMs);
                addCleanup(() => clearTimeout(timer));
            });
        };
    }
}

export const defaultUpdateDelayer: UpdateDelayer = FixedDelayer.get(1000 / 60);
