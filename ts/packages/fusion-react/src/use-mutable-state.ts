import { useEffect, useReducer, useRef } from 'react';
import { MutableState } from '@actuallab/fusion';
import type { Result } from '@actuallab/core';

export interface UseMutableStateResult<T> {
    value: T | undefined;
    error: unknown;
    set: (value: Result<T> | T) => void;
    state: MutableState<T>;
}

/**
 * React hook wrapping Fusion's MutableState.
 * Returns { value, error, set, state } so an error result renders instead of throwing.
 */
export function useMutableState<T>(initial: T): UseMutableStateResult<T> {
    const [, forceRender] = useReducer(c => c + 1, 0);
    const stateRef = useRef<MutableState<T> | null>(null);

    stateRef.current ??= new MutableState<T>(initial);

    const state = stateRef.current;

    useEffect(() => {
        let cancelled = false;

        const subscribe = async () => {
            while (!cancelled) {
                const sinceIndex = state.updateIndex;
                try {
                    await state.whenUpdated(sinceIndex);
                } catch {
                    return;
                }
                // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
                if (!cancelled) forceRender();
            }
        };

        void subscribe();

        return () => {
            cancelled = true;
        };
    }, [state]);

    return {
        value: state.valueOrUndefined,
        error: state.error,
        set: v => state.set(v),
        state,
    };
}
