import { useCallback, useRef, useSyncExternalStore } from 'react';
import {
    ComputedState,
    type ComputedStateOptions,
    type StateComputer,
} from '@actuallab/fusion';

export interface UseComputedStateResult<T> {
    value: T | undefined;
    error: unknown;
    isInitial: boolean;
    state: ComputedState<T> | undefined;
}

/**
 * React hook wrapping Fusion's ComputedState, built on useSyncExternalStore so
 * updates between render and subscription are never lost and rendering is tear-free.
 * The ComputedState is created and disposed exclusively inside `subscribe` (keyed on
 * deps), so a discarded concurrent render leaks nothing and a StrictMode
 * mount→cleanup→remount rebuilds a live state instead of freezing on a disposed one.
 */
export function useComputedState<T>(
    computer: StateComputer<T>,
    deps: readonly unknown[],
    options?: ComputedStateOptions<T>
): UseComputedStateResult<T> {
    const stateRef = useRef<ComputedState<T> | null>(null);
    const generationRef = useRef(0);

    const subscribe = useCallback(
        (onStoreChange: () => void) => {
            const state = new ComputedState<T>(computer, options);
            generationRef.current++;
            stateRef.current = state;
            onStoreChange();
            let cancelled = false;
            void (async () => {
                // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
                while (!cancelled && !state.isDisposed) {
                    const sinceIndex = state.updateIndex;
                    try {
                        await state.whenUpdated(sinceIndex);
                    } catch {
                        return;
                    }
                    // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
                    if (!cancelled && !state.isDisposed)
                        onStoreChange();
                }
            })();

            return () => {
                cancelled = true;
                state.dispose();
                if (stateRef.current === state)
                    stateRef.current = null;
            };
        },
        deps
    );

    // Snapshot is `generation:updateIndex` — the generation makes a rebuilt state
    // distinct even at the same updateIndex (a sync computer reaches index 1 inside
    // the constructor, so a deps-change replacement would otherwise collide with the
    // rendered snapshot and never re-render). '' marks "no state yet" (the pre-effect
    // first render), which the built-in re-check upgrades once `subscribe` creates one.
    useSyncExternalStore(
        subscribe,
        () =>
            stateRef.current === null
                ? ''
                : `${generationRef.current}:${stateRef.current.updateIndex}`,
        () => ''
    );

    const state = stateRef.current ?? undefined;
    if (state === undefined || state.updateIndex === 0)
        return { value: undefined, error: undefined, isInitial: true, state };

    return {
        value: state.valueOrUndefined,
        error: state.error,
        isInitial: false,
        state,
    };
}
