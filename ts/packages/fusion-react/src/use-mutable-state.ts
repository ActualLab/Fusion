import { useEffect, useReducer, useRef } from "react";
import { MutableState } from "@actuallab/fusion";
import type { Result } from "@actuallab/core";

/**
 * React hook wrapping Fusion's MutableState.
 * Returns [value, setter, state] — re-renders on updates.
 */
export function useMutableState<T>(
  initial: T,
): [T, (value: Result<T> | T) => void, MutableState<T>] {
  const [, forceRender] = useReducer(c => c + 1, 0);
  const stateRef = useRef<MutableState<T> | null>(null);

  if (stateRef.current === null) {
    stateRef.current = new MutableState<T>(initial);
  }

  const state = stateRef.current;

  useEffect(() => {
    let cancelled = false;

    const subscribe = async () => {
      while (!cancelled) {
        try {
          await state.whenUpdated();
        } catch {
          return;
        }
        if (!cancelled)
          forceRender();
      }
    };

    void subscribe();

    return () => { cancelled = true; };
  }, [state]);

  return [state.value, (v) => state.set(v), state];
}
