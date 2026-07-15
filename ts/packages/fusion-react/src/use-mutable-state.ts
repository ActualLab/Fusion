import { useCallback, useRef, useSyncExternalStore } from 'react';
import { MutableState } from '@actuallab/fusion';
import type { Result } from '@actuallab/core';

export interface UseMutableStateResult<T> {
    value: T | undefined;
    error: unknown;
    set: (value: Result<T> | T) => void;
    state: MutableState<T>;
}

/**
 * React hook wrapping Fusion's MutableState, built on useSyncExternalStore so a
 * `set` landing between render and subscription is never lost. `value` is
 * `state.valueOrUndefined` and never throws on an error result; the error is
 * exposed separately.
 */
export function useMutableState<T>(initial: T): UseMutableStateResult<T> {
    const stateRef = useRef<MutableState<T> | null>(null);
    stateRef.current ??= new MutableState<T>(initial);
    const state = stateRef.current;

    const subscribe = useCallback(
        (onStoreChange: () => void) => {
            let cancelled = false;
            void (async () => {
                // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
                while (!cancelled) {
                    const sinceIndex = state.updateIndex;
                    try {
                        await state.whenUpdated(sinceIndex);
                    } catch {
                        return;
                    }
                    // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
                    if (!cancelled)
                        onStoreChange();
                }
            })();

            return () => {
                cancelled = true;
            };
        },
        [state]
    );

    useSyncExternalStore(
        subscribe,
        () => state.updateIndex,
        () => state.updateIndex
    );

    return {
        value: state.valueOrUndefined,
        error: state.error,
        set: (value: Result<T> | T) => state.set(value),
        state,
    };
}
