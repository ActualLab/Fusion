import { delayAsync } from './delay.js';
import { getLogs } from './internal-logging.js';

const { warnLog } = getLogs('retry');

/** A sequence of retry delays — given a 1-based try index (1 = first
 *  retry, i.e. AFTER the initial failure), returns the delay in ms before
 *  the next attempt. For more elaborate delay schemes, see
 *  {@link RetryDelaySeq.getDelay}. */
export type RetryDelaySchedule = (tryIndex: number) => number;

const defaultRetryDelays: RetryDelaySchedule = () => 50;

/**
 * Run `fn` up to `tryCount` times, awaiting a delay between attempts.
 * The last error is rethrown if every attempt fails.
 *
 * `fn` is passed the 0-based attempt index and the last error (or
 * `undefined` for the first attempt) — useful for adapting behavior per
 * try (e.g. switching endpoints, escalating timeouts).
 *
 * Aborting `signal` during an inter-attempt delay rejects with
 * `signal.reason` instead of waiting the delay out.
 */
export async function retry<TResult>(
    tryCount: number,
    fn: (tryIndex: number, lastError: unknown) => PromiseLike<TResult> | TResult,
    retryDelays: RetryDelaySchedule = defaultRetryDelays,
    signal?: AbortSignal,
): Promise<TResult> {
    if (tryCount <= 0)
        throw new Error('retry: tryCount must be positive.');

    let lastError: unknown;
    for (let tryIndex = 0; ; ) {
        if (tryIndex >= tryCount) throw lastError;
        try {
            return await fn(tryIndex, lastError);
        } catch (e) {
            lastError = e;
        }
        ++tryIndex;
        if (tryIndex >= tryCount) throw lastError;
        warnLog?.log(`retry(${tryIndex}/${tryCount}): error:`, lastError);
        await delayAsync(retryDelays(tryIndex), signal);
    }
}

/**
 * Run `fn` and swallow any throw, returning `onError(e)` if provided or
 * `undefined` otherwise. The returned promise always settles successfully
 * — the caller never has to `try/catch` it.
 *
 * Useful where a logical "best-effort" return is wanted but the runtime
 * defaults — unhandled rejection warnings, exception escape past finally
 * blocks — are not.
 */
export async function catchErrors<TResult>(
    fn: () => PromiseLike<TResult> | TResult,
    onError?: (e: unknown) => TResult,
): Promise<TResult | undefined> {
    try {
        return await fn();
    } catch (e) {
        return onError ? onError(e) : undefined;
    }
}
