/**
 * Adapt an `AbortSignal` to a Promise that **rejects** with the signal's
 * `reason` once the signal fires (or immediately, if it's already
 * aborted). Mirrors `signal.throwIfAborted()` semantics in Promise form:
 * the rejected value is exactly what `throwIfAborted()` would have
 * thrown — by default a `DOMException` with `name === 'AbortError'`,
 * or whatever was passed to `abortController.abort(reason)`.
 *
 * Operators racing upstream pulls / timers against cancellation plug
 * this into `Promise.race(...)` so abort propagates as a thrown error —
 * `try/finally` blocks unwind naturally; no sentinel values to thread.
 *
 * Promises are cached per signal: every call for the same signal returns
 * the SAME promise and registers AT MOST ONE listener. Operators that
 * race per-iteration (e.g. encode loops calling this once per chunk)
 * won't accumulate listeners or hit Node's `MaxListenersExceeded` warning.
 * The cache is a `WeakMap` so signals remain GC-eligible normally.
 *
 * Callers that prefer a sentinel can attach `.catch()` to convert the
 * rejection back into a value: `abortPromise(s).catch(() => 'stopped')`.
 */
const abortPromiseCache = new WeakMap<AbortSignal, Promise<never>>();

export function abortPromise(signal: AbortSignal): Promise<never> {
    // eslint-disable-next-line @typescript-eslint/prefer-promise-reject-errors -- mirroring `signal.throwIfAborted()`: the rejection value IS the signal's reason, which by Web spec is a `DOMException` by default but can be any value the caller passed to `abort(reason)`.
    if (signal.aborted) return Promise.reject(signal.reason);
    let cached = abortPromiseCache.get(signal);
    if (!cached) {
        cached = new Promise<never>((_, reject) => {
            // eslint-disable-next-line @typescript-eslint/prefer-promise-reject-errors -- see comment above; signal.reason is the canonical thrown value.
            signal.addEventListener('abort', () => reject(signal.reason), { once: true });
        });
        // Mark the cached rejection as observed so unhandled-rejection
        // warnings don't fire when a caller never `await`s it (e.g. an
        // operator that completed before abort and let the race promise
        // GC). The actual await-time rejection still surfaces normally.
        cached.catch(() => { /* swallow for cache-only consumers */ });
        abortPromiseCache.set(signal, cached);
    }
    return cached;
}
