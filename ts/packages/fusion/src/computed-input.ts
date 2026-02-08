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

/** Base class for all state types — serves as ComputedInput identity by reference. */
export abstract class StateBase {}

/**
 * Identity key for any computed value.
 * - String for compute method results (built from instance + function + args)
 * - StateBase for state types (identity by reference)
 */
export type ComputedInput = string | StateBase;
