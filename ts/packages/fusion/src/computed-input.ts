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

/** Base — identity key for any computed value. States inherit from this directly. */
export class ComputedInput {
  readonly key: string;

  constructor(key: string) {
    this.key = key;
  }

  equals(other: ComputedInput): boolean {
    return this.key === other.key;
  }

  toString(): string {
    return this.key;
  }
}

/** Extended input for compute methods — adds instance ref, method name, args. */
export class ComputeMethodInput extends ComputedInput {
  readonly instance: object;
  readonly methodName: string;
  readonly args: unknown[];

  constructor(instance: object, methodName: string, args: unknown[]) {
    const instanceId = getInstanceId(instance);
    super(`${instanceId}.${methodName}:${args.map(a => JSON.stringify(a)).join(",")}`);
    this.instance = instance;
    this.methodName = methodName;
    this.args = args;
  }
}
