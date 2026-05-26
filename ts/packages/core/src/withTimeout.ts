/** Error type rejected by {@link withTimeout} when its budget elapses. */
export class TimeoutError extends Error {
    override readonly name = 'TimeoutError';

    constructor(message = 'Operation timed out') {
        super(message);
    }
}

/**
 * Race `promise` against a `ms`-millisecond timer. Resolves with the inner
 * promise's value if it settles first; otherwise rejects with a
 * {@link TimeoutError} carrying `message`. The timer is always cleared in
 * a `finally`, so it cannot keep the event loop alive after the race ends.
 *
 * The inner promise's rejection (and the timeout's) are routed through
 * `Promise.race`, so callers should `await` the result inside a `try/catch`
 * exactly as they would for a single awaited promise. There's no
 * "unhandled rejection" hazard from the timeout branch — the race always
 * observes whichever promise wins, and the loser is allowed to settle
 * silently into the void.
 */
export async function withTimeout<T>(
    promise: PromiseLike<T>,
    ms: number,
    message?: string
): Promise<T> {
    let timer: ReturnType<typeof setTimeout> | undefined;
    const timeoutPromise = new Promise<never>((_, reject) => {
        timer = setTimeout(
            () => reject(new TimeoutError(message ?? `Operation timed out after ${ms}ms`)),
            ms
        );
    });
    // Routed via `Promise.race` below; suppress the standalone unhandled-
    // rejection warning that fires when `promise` wins instead.
    timeoutPromise.catch(() => { /* noop */ });
    try {
        return await Promise.race([promise, timeoutPromise]);
    } finally {
        if (timer !== undefined) clearTimeout(timer);
    }
}
