/** Resolves after `ms` milliseconds. */
export function delayAsync(ms: number): Promise<void> {
    return new Promise<void>(resolve => {
        setTimeout(resolve, ms);
    });
}

/** Resolves with `value` after `ms` milliseconds. */
export function delayAsyncWith<T>(ms: number, value: T): Promise<T> {
    return new Promise<T>(resolve => {
        setTimeout(() => resolve(value), ms);
    });
}
