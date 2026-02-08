import { AsyncContext, AsyncLock, type Result, ok, error } from "@actuallab/core";
import { Computed } from "./computed.js";
import { getInstanceId } from "./computed-input.js";
import { ComputeContext, computeContextKey } from "./compute-context.js";
import { computedRegistry } from "./computed-registry.js";

export type ComputeFn = (this: any, ...args: unknown[]) => unknown;

/** Record Separator — delimiter between key components. */
const RS = "\x1E";

function defaultArgToString(arg: unknown): string {
  return JSON.stringify(arg) ?? "undefined";
}

let _nextFunctionId = 0;

/** Produces Computed<T> from a function — handles cache lookup, locking, dependency capture. */
export class ComputeFunction {
  readonly methodName: string;
  readonly id: string;
  argToString: (arg: unknown) => string = defaultArgToString;
  private _fn: ComputeFn;
  private _locks = new Map<string, AsyncLock>();

  constructor(methodName: string, fn: ComputeFn) {
    this.methodName = methodName;
    this.id = `${methodName}[${++_nextFunctionId}]`;
    this._fn = fn;
  }

  /** Build a string key for the given instance and arguments. */
  buildKey(instance: object, args: unknown[]): string {
    const parts: string[] = [String(getInstanceId(instance)), this.id];
    for (const arg of args) {
      parts.push(this.argToString(arg));
    }
    return parts.join(RS);
  }

  async invoke(instance: object, allArgs: unknown[]): Promise<Computed<unknown>> {
    // 1. Resolve AsyncContext ONCE via DRY helper
    const asyncCtx = AsyncContext.fromArgs(allArgs);

    // 2. Pull caller's ComputeContext directly
    const callerComputeCtx = asyncCtx?.get(computeContextKey);

    // 3. Strip THIS context from args (reference equality — safe)
    const args = asyncCtx?.stripFromArgs(allArgs) ?? allArgs;

    // 4. Build string key and check cache
    const key = this.buildKey(instance, args);
    const existing = computedRegistry.get(key);
    if (existing?.isConsistent) {
      callerComputeCtx?.captureDependency(existing);
      return existing;
    }

    // 5. Lock per key to prevent duplicate computations
    let lock = this._locks.get(key);
    if (lock === undefined) {
      lock = new AsyncLock();
      this._locks.set(key, lock);
    }

    const computed = await lock.run(async () => {
      // Double-check after acquiring lock
      const cached = computedRegistry.get(key);
      if (cached?.isConsistent) return cached;

      // 6. Create new Computed + ComputeContext
      const newComputed = new Computed<unknown>(key);
      computedRegistry.register(newComputed);

      const childComputeCtx = new ComputeContext(newComputed);
      const childAsyncCtx = (asyncCtx ?? AsyncContext.empty)
        .with(computeContextKey, childComputeCtx);

      // 7. Run with clean args — no stale AsyncContext to override .run()
      let result: Result<unknown>;
      try {
        const value = childAsyncCtx.run(() => this._fn.call(instance, ...args));
        const resolved = value instanceof Promise ? await value : value;
        result = ok(resolved);
      } catch (e) {
        result = error(e);
      }

      if (result.ok) newComputed.setOutput(result.value);
      else newComputed.setError(result.error);

      return newComputed;
    });

    // Register dependency from caller to the produced computed
    callerComputeCtx?.captureDependency(computed);
    return computed;
  }
}
