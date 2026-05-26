/**
 * Wrap an async function so concurrent invocations are serialized into a
 * single chain — each call awaits the previous one before running, and the
 * caller receives the result of *its* own invocation.
 *
 * Useful for stateful async operations (DB writes, file I/O, network I/O
 * that must not overlap) where the underlying contract is "one at a time."
 *
 * If `limit` is set and the queue is already that deep, the call short-
 * circuits and returns the same promise the most recent queued call returned
 * — i.e. it coalesces excess pressure onto the trailing in-flight call.
 * Pass `null` (the default) for unbounded queueing.
 */
export function serialize<TArgs extends unknown[], TResult>(
    func: (...args: TArgs) => PromiseLike<TResult> | TResult,
    limit: number | null = null
): (...args: TArgs) => Promise<TResult> {
    let lastCall: Promise<TResult> = Promise.resolve(null as TResult);
    let queueSize = 0;

    return function (this: unknown, ...args: TArgs): Promise<TResult> {
        if (limit !== null && queueSize >= limit) return lastCall;

        queueSize++;
        const prevCall = lastCall;
        // Capture `this` via the bound caller chain so we don't have to alias
        // `this` to a local (which the linter flags). `Reflect.apply` preserves
        // the function's typed return value (unlike `Function.prototype.apply`,
        // which is declared as returning `any`).
        const invoke = (): PromiseLike<TResult> | TResult => Reflect.apply(func, this, args);
        return lastCall = (async () => {
            try {
                // Wait for the previous call to settle — but don't inherit its
                // rejection. The previous caller already owns that error; if we
                // re-threw here, a single failure would poison the queue
                // forever (every subsequent call awaits the previous one).
                await prevCall.catch(() => { /* see comment above */ });
                return await invoke();
            } finally {
                queueSize--;
            }
        })();
    };
}
