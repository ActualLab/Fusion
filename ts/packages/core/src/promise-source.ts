/** Externally-resolvable promise — similar to .NET's TaskCompletionSource<T>. */
export class PromiseSource<T> implements Promise<T> {
    readonly promise: Promise<T>;
    readonly [Symbol.toStringTag] = 'Promise';
    private _resolve!: (value: T | PromiseLike<T>) => void;
    private _reject!: (reason?: unknown) => void;
    private _isCompleted = false;

    constructor() {
        this.promise = new Promise<T>((resolve, reject) => {
            this._resolve = resolve;
            this._reject = reject;
        });
    }

    get isCompleted(): boolean {
        return this._isCompleted;
    }

    resolve(value: T): boolean {
        if (this._isCompleted) return false;
        this._isCompleted = true;
        this._resolve(value);
        return true;
    }

    reject(reason?: unknown): boolean {
        if (this._isCompleted) return false;
        this._isCompleted = true;
        this._reject(reason);
        return true;
    }

    then<TResult1 = T, TResult2 = never>(
        onfulfilled?: ((value: T) => TResult1 | PromiseLike<TResult1>) | null,
        onrejected?: ((reason: unknown) => TResult2 | PromiseLike<TResult2>) | null,
    ): Promise<TResult1 | TResult2> {
        return this.promise.then(onfulfilled, onrejected);
    }

    catch<TResult = never>(
        onrejected?: ((reason: unknown) => TResult | PromiseLike<TResult>) | null,
    ): Promise<T | TResult> {
        return this.promise.catch(onrejected);
    }

    finally(onfinally?: (() => void) | null): Promise<T> {
        return this.promise.finally(onfinally);
    }
}

export const resolvedVoidPromise: Promise<void> = Promise.resolve();
