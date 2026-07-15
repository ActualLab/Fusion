/** Promise-based mutual exclusion lock for serializing async operations. */
export class AsyncLock {
    private _queue: (() => void)[] = [];
    private _locked = false;

    get isLocked(): boolean {
        return this._locked;
    }

    async acquire(signal?: AbortSignal): Promise<void> {
        if (signal?.aborted)
            throw signal.reason;

        if (!this._locked) {
            this._locked = true;
            return;
        }
        return new Promise<void>((resolve, reject) => {
            const onAbort = () => {
                const i = this._queue.indexOf(waiter);
                if (i >= 0)
                    this._queue.splice(i, 1);

                // eslint-disable-next-line @typescript-eslint/prefer-promise-reject-errors -- the rejection value IS signal.reason.
                reject(signal!.reason);
            };
            const waiter = () => {
                signal?.removeEventListener('abort', onAbort);
                resolve();
            };
            this._queue.push(waiter);
            signal?.addEventListener('abort', onAbort, { once: true });
        });
    }

    release(): void {
        if (!this._locked) throw new Error('AsyncLock is not locked.');

        const next = this._queue.shift();
        if (next !== undefined) {
            // Transfer lock to the next waiter
            next();
        } else {
            this._locked = false;
        }
    }

    async run<T>(fn: () => T | Promise<T>, signal?: AbortSignal): Promise<T> {
        await this.acquire(signal);
        try {
            return await fn();
        } finally {
            this.release();
        }
    }
}
