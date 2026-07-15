import { PromiseSource, resolvedVoidPromise } from './promise-source.js';

export type AbortOutcome = 'resolve' | 'reject';

/**
 * Builds a Promise that settles when the first completion source fires, then runs
 * every registered cleanup exactly once — on completion or when `abortSignal`
 * aborts. `wire` installs the sources (timers, event handlers), calling
 * `complete()` to resolve and `addCleanup(fn)` to register teardown; every
 * cleanup, plus the abort listener, is removed on every exit path. `onAbort`
 * selects whether an abort resolves or rejects (with `signal.reason`).
 */
export function awaitWithCleanup(
    abortSignal: AbortSignal | undefined,
    onAbort: AbortOutcome,
    wire: (complete: () => void, addCleanup: (cleanup: () => void) => void) => void,
): Promise<void> {
    if (abortSignal?.aborted)
        return onAbort === 'reject'
            // eslint-disable-next-line @typescript-eslint/prefer-promise-reject-errors -- mirrors signal.throwIfAborted(): the rejection value IS signal.reason.
            ? Promise.reject(abortSignal.reason ?? new Error('Operation cancelled.'))
            : resolvedVoidPromise;

    const ps = new PromiseSource<void>();
    const cleanups: (() => void)[] = [];
    let settled = false;
    const runCleanups = () => {
        for (const cleanup of cleanups)
            cleanup();

        cleanups.length = 0;
    };
    const complete = () => {
        if (settled)
            return;

        settled = true;
        runCleanups();
        ps.resolve(undefined);
    };
    const addCleanup = (cleanup: () => void) => {
        if (settled)
            cleanup();
        else
            cleanups.push(cleanup);
    };

    if (abortSignal !== undefined) {
        const onAbortFired = () => {
            if (settled)
                return;

            settled = true;
            runCleanups();
            if (onAbort === 'reject')
                ps.reject(abortSignal.reason ?? new Error('Operation cancelled.'));
            else
                ps.resolve(undefined);
        };
        abortSignal.addEventListener('abort', onAbortFired, { once: true });
        cleanups.push(() => abortSignal.removeEventListener('abort', onAbortFired));
    }

    wire(complete, addCleanup);
    return ps;
}
