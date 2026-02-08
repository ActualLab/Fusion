import type { State } from "./state.js";

/** Auto-assigned numeric IDs for instance identity in compute method cache keys. */
let _nextInstanceId = 0;
const _instanceIds = new WeakMap<object, number>();

export function getInstanceId(instance: object): number {
  let id = _instanceIds.get(instance);
  if (id === undefined) {
    id = ++_nextInstanceId;
    _instanceIds.set(instance, id);
  }
  return id;
}

/**
 * Identity key for any computed value.
 * - String for compute method results (built from instance + function + args)
 * - State for state types (identity by reference)
 */
export type ComputedInput = string | State<any>;
