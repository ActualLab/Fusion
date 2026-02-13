import { useEffect, useReducer, useRef } from "react";
import { ComputedState, type ComputedStateOptions, type StateComputer } from "@actuallab/fusion";

export interface UseComputedStateResult<T> {
  value: T | undefined;
  error: unknown;
  isInitial: boolean;
  state: ComputedState<T>;
}

/**
 * React hook wrapping Fusion's ComputedState.
 * Creates a ComputedState keyed on deps, subscribes to updates, and triggers re-renders.
 */
export function useComputedState<T>(
  computer: StateComputer<T>,
  deps: readonly unknown[],
  options?: ComputedStateOptions<T>,
): UseComputedStateResult<T> {
  const [, forceRender] = useReducer(c => c + 1, 0);
  const stateRef = useRef<ComputedState<T> | null>(null);
  const depsRef = useRef<readonly unknown[]>(deps);

  // Check if deps changed
  const depsChanged = deps.length !== depsRef.current.length
    || deps.some((d, i) => d !== depsRef.current[i]);

  if (depsChanged || stateRef.current === null) {
    // Dispose old state
    stateRef.current?.dispose();
    depsRef.current = deps;
    stateRef.current = new ComputedState<T>(computer, options);
  }

  const state = stateRef.current;

  useEffect(() => {
    let cancelled = false;

    const subscribe = async () => {
      // Wait for first update
      try {
        await state.whenFirstTimeUpdated();
      } catch {
        return;
      }
      if (cancelled || state.isDisposed) return;
      forceRender();

      // Subscribe to subsequent updates
      while (!cancelled && !state.isDisposed) {
        try {
          await state.whenUpdated();
        } catch {
          return;
        }
        if (!cancelled && !state.isDisposed)
          forceRender();
      }
    };

    void subscribe();

    return () => {
      cancelled = true;
      state.dispose();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  const isInitial = state.updateIndex === 0;
  return {
    value: isInitial ? undefined : state.valueOrUndefined,
    error: isInitial ? undefined : state.error,
    isInitial,
    state,
  };
}
