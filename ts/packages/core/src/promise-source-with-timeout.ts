import { PromiseSource } from './promise-source.js';
import { TimeoutError } from './withTimeout.js';

/**
 * A {@link PromiseSource} with a built-in timer that can reject (or run a
 * caller-supplied callback) after a configurable delay. The timer is
 * automatically cleared when the promise settles by other means, so callers
 * don't have to remember to `clearTimeout()` on every success path.
 *
 * Typical use:
 * ```ts
 * const ps = new PromiseSourceWithTimeout<string>();
 * ps.setTimeout(5_000);                  // rejects after 5s if still pending
 * ps.setTimeout(5_000, () => ps.resolve('fallback')); // or run a custom callback
 * ```
 *
 * Replaces the `PromiseSourceWithTimeout` previously kept in
 * `ActualChat/src/nodejs/src/promises.ts`. Note: the Voxt original supported a
 * "precise" (requestAnimationFrame-based) variant; we drop it here because no
 * caller in ActualChat uses it outside `promises.ts` itself.
 */
export class PromiseSourceWithTimeout<T> extends PromiseSource<T> {
    private _timer: ReturnType<typeof setTimeout> | undefined;

    hasTimeout(): boolean {
        return this._timer !== undefined;
    }

    /**
     * Install (or replace) a timeout. Pass `null` to remove without setting a
     * new one. `callback` is invoked when the timer fires — if omitted, the
     * promise is rejected with a generic "timed out" error.
     */
    setTimeout(ms: number | null, callback?: () => unknown): void {
        this.clearTimeout();
        if (ms === null || this.isCompleted) return;

        this._timer = setTimeout(() => {
            this._timer = undefined;
            if (callback) callback();
            else this.reject(new TimeoutError(`The promise has timed out after ${ms}ms.`));
        }, ms);
    }

    clearTimeout(): void {
        if (this._timer === undefined) return;
        clearTimeout(this._timer);
        this._timer = undefined;
    }

    override resolve(value: T): boolean {
        const didSettle = super.resolve(value);
        if (didSettle) this.clearTimeout();
        return didSettle;
    }

    override reject(reason?: unknown): boolean {
        const didSettle = super.reject(reason);
        if (didSettle) this.clearTimeout();
        return didSettle;
    }
}
