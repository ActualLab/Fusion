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

let _nextStateId = 0;

/** Base class for all state types — serves as ComputedInput identity by reference. */
export abstract class StateBase {
  readonly stateKey: string;

  constructor(prefix: string) {
    this.stateKey = `${prefix}#${++_nextStateId}`;
  }
}

/**
 * Identity key for any computed value.
 * - String for compute method results (built from instance + function + args)
 * - StateBase for state types (identity by reference)
 */
export type ComputedInput = string | StateBase;

/** Extract a string key from a ComputedInput. */
export function inputKey(input: ComputedInput): string {
  return typeof input === "string" ? input : input.stateKey;
}
