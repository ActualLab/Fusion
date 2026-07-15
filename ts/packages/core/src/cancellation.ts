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

/**
 * Builds a cancellation-shaped error recognized by {@link isCancellation}.
 * Named `'AbortError'` so it rides the same never-cache path as an aborted
 * `fetch`: the compute kernel invalidates such computeds instead of caching
 * the error (K6), and RPC callers treat it as a retryable cancellation.
 */
export function cancellationError(message = 'The operation was cancelled.'): Error {
    const error = new Error(message);
    error.name = 'AbortError';
    return error;
}
