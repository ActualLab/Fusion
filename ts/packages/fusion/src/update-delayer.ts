import { awaitWithCleanup } from '@actuallab/core';

/** Controls when state re-computation happens after invalidation. */
export type UpdateDelayer = (abortSignal?: AbortSignal) => Promise<void>;

/** Re-computes after a fixed delay in milliseconds. */
export class FixedDelayer {
    static readonly zero: UpdateDelayer = () => Promise.resolve();

    private static _cache = new Map<number, UpdateDelayer>();

    static get(ms: number): UpdateDelayer {
        if (ms <= 0) return FixedDelayer.zero;
        let delayer = FixedDelayer._cache.get(ms);
        if (delayer === undefined) {
            delayer = new FixedDelayer(ms).delay;
            FixedDelayer._cache.set(ms, delayer);
        }
        return delayer;
    }

    readonly ms: number;
    readonly delay: UpdateDelayer;

    constructor(ms: number) {
        this.ms = ms;
        // Abort stops the wait early (resolves, never rejects) so a disposed
        // ComputedState's update loop terminates promptly and the race loser
        // carries no unhandled rejection (C# SuppressCancellationAwait spirit).
        this.delay = (abortSignal?: AbortSignal) =>
            awaitWithCleanup(abortSignal, 'resolve', (complete, addCleanup) => {
                const timer = setTimeout(complete, this.ms);
                addCleanup(() => clearTimeout(timer));
            });
    }
}

export const defaultUpdateDelayer: UpdateDelayer = FixedDelayer.get(1000 / 60);
