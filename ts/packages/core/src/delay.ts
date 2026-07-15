/** Resolves after `ms` milliseconds, or rejects with `signal.reason` if
 *  `signal` aborts first (including when it is already aborted). */
export function delayAsync(ms: number, signal?: AbortSignal): Promise<void> {
    if (signal?.aborted)
        // eslint-disable-next-line @typescript-eslint/prefer-promise-reject-errors -- mirrors signal.throwIfAborted(): the rejection value IS signal.reason.
        return Promise.reject(signal.reason);

    return new Promise<void>((resolve, reject) => {
        const onAbort = () => {
            clearTimeout(timer);
            // eslint-disable-next-line @typescript-eslint/prefer-promise-reject-errors -- see delayAsync's already-aborted branch.
            reject(signal!.reason);
        };
        const timer = setTimeout(() => {
            signal?.removeEventListener('abort', onAbort);
            resolve();
        }, ms);
        signal?.addEventListener('abort', onAbort, { once: true });
    });
}

/** Resolves with `value` after `ms` milliseconds. */
export function delayAsyncWith<T>(ms: number, value: T): Promise<T> {
    return new Promise<T>(resolve => {
        setTimeout(() => resolve(value), ms);
    });
}
