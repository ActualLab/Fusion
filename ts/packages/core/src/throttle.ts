import { getLogs } from './internal-logging.js';

const { errorLog } = getLogs('throttle');

/** A function wrapped by {@link throttle} or {@link debounce}. Calling it
 *  triggers the underlying function according to the scheduling rule;
 *  `reset()` discards any pending scheduled call. */
export interface ResettableFunc<TArgs extends unknown[]> {
    (...args: TArgs): void;
    reset(): void;
}

class Call<TArgs extends unknown[]> {
    constructor(
        readonly func: (...args: TArgs) => unknown,
        readonly self: unknown,
        readonly parameters: TArgs
    ) { }

    invoke(): unknown {
        return this.func.apply(this.self, this.parameters);
    }

    invokeSilently(): unknown {
        try {
            return this.invoke();
        } catch (error) {
            errorLog?.log('Call.invokeSilently: unhandled error:', error);
        }
    }
}

/** Throttle modes:
 *  - `default` — fire the head call immediately, then drop further calls
 *    until the interval elapses; the *last* one is fired at interval end.
 *  - `skip` — fire the head call immediately, drop ALL calls during the
 *    interval (no tail call).
 *  - `delayHead` — delay even the head call by one interval; subsequent
 *    calls overwrite the pending one. */
export type ThrottleMode = 'default' | 'skip' | 'delayHead';

/**
 * Throttle calls to `func` so that it fires at most once every `intervalMs`.
 * The returned function has a `.reset()` that discards any pending tail call
 * and re-arms the head-call behavior on the next invocation.
 */
export function throttle<TArgs extends unknown[]>(
    func: (...args: TArgs) => unknown,
    intervalMs: number,
    mode: ThrottleMode = 'default'
): ResettableFunc<TArgs> {
    let lastCall: Call<TArgs> | null = null;
    let nextFireTime = 0;
    let timeoutHandle: ReturnType<typeof setTimeout> | null = null;

    const reset = (): void => {
        if (timeoutHandle !== null) clearTimeout(timeoutHandle);
        timeoutHandle = null;
        lastCall = null;
        nextFireTime = 0;
    };

    const fire = (): void => {
        if (timeoutHandle !== null) clearTimeout(timeoutHandle);

        if (lastCall !== null) {
            const call = lastCall;
            lastCall = null;
            nextFireTime = Date.now() + intervalMs;
            timeoutHandle = setTimeout(fire, intervalMs);
            call.invokeSilently(); // Must be last
        } else {
            timeoutHandle = null;
            nextFireTime = 0;
        }
    };

    const wrapped: ResettableFunc<TArgs> = function (this: unknown, ...args: TArgs): void {
        const call = new Call<TArgs>(func, this, args);
        const fireDelay = nextFireTime - Date.now();
        if (timeoutHandle !== null && fireDelay <= 0) {
            // Delayed "fire" is ready but not yet executed — flush it now.
            fire();
        }

        if (timeoutHandle === null) {
            // lastCall is null here
            nextFireTime = Date.now() + intervalMs;
            timeoutHandle = setTimeout(fire, intervalMs);
            if (mode === 'delayHead') {
                lastCall = call;
            } else {
                call.invokeSilently();
            }
        } else if (mode !== 'skip') {
            lastCall = call;
        }
    };
    wrapped.reset = reset;
    return wrapped;
}

/**
 * Debounce calls to `func`: the function fires only after `intervalMs` have
 * elapsed since the LAST call. Each new call resets the timer. `.reset()`
 * discards any pending fire without invoking `func`.
 */
export function debounce<TArgs extends unknown[]>(
    func: (...args: TArgs) => unknown,
    intervalMs: number
): ResettableFunc<TArgs> {
    let lastCall: Call<TArgs> | null = null;
    let nextFireTime = 0;
    let timeoutHandle: ReturnType<typeof setTimeout> | null = null;

    const reset = (): void => {
        // Don't clearTimeout here: the live timer will see `lastCall === null`
        // and exit quietly. Avoids a churn of cleared-then-set timers when
        // callers `reset()` then immediately call again.
        timeoutHandle = null;
        lastCall = null;
        nextFireTime = 0;
    };

    const fire = (): void => {
        if (timeoutHandle !== null) clearTimeout(timeoutHandle);
        timeoutHandle = null;

        const fireDelay = nextFireTime - Date.now();
        if (fireDelay <= 0) {
            nextFireTime = 0;
            if (lastCall !== null) {
                const call = lastCall;
                lastCall = null;
                call.invokeSilently(); // Must be last
            }
        } else {
            // Another call landed; reschedule for the new deadline.
            timeoutHandle = setTimeout(fire, fireDelay);
        }
    };

    const wrapped: ResettableFunc<TArgs> = function (this: unknown, ...args: TArgs): void {
        lastCall = new Call<TArgs>(func, this, args);
        nextFireTime = Date.now() + intervalMs;
        timeoutHandle ??= setTimeout(fire, intervalMs);
    };
    wrapped.reset = reset;
    return wrapped;
}
