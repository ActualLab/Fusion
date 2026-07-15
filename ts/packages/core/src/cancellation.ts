/**
 * True when `error` is a cancellation/abort signal rather than a real failure.
 *
 * Recognizes the wave-1 cancellation convention: `delayAsync`, `AsyncLock`, and
 * `abortPromise` all reject with `signal.reason`, which — per the Web spec —
 * defaults to a `DOMException` named `'AbortError'` (and is whatever value was
 * passed to `abort(reason)` otherwise). A plain `Error` named `'AbortError'`
 * (e.g. from a fetch abort) is matched too.
 */
export function isCancellation(error: unknown): boolean {
    return (
        error != null &&
        typeof error === 'object' &&
        (error as { name?: unknown }).name === 'AbortError'
    );
}
