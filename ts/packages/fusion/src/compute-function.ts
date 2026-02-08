import { AsyncContext, AsyncLock, type Result, result, errorResult } from "@actuallab/core";
import { Computed } from "./computed.js";
import { getInstanceId } from "./computed-input.js";
import { ComputeContext, computeContextKey } from "./compute-context.js";
import { ComputedRegistry } from "./computed-registry.js";

export type ComputeFunctionImpl = (this: any, ...args: unknown[]) => unknown;

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
  private _impl: ComputeFunctionImpl;
  private _locks = new Map<string, AsyncLock>();

  constructor(methodName: string, impl: ComputeFunctionImpl) {
    this.methodName = methodName;
    this.id = `${methodName}[${++_nextFunctionId}]`;
    this._impl = impl;
  }

  /** Build a string key for the given instance and arguments. */
  buildKey(instance: object, args: unknown[]): string {
    let key = String(getInstanceId(instance)) + RS + this.id;
    for (const arg of args) {
      key += RS + this.argToString(arg);
    }
    return key;
  }

  async invoke(instance: object, args: unknown[], prevComputed?: Computed<unknown>): Promise<Computed<unknown>> {
    // 1. Resolve AsyncContext ONCE via DRY helper
    const asyncCtx = AsyncContext.fromArgs(args);

    // 2. Pull caller's ComputeContext directly
    const callerComputeCtx = asyncCtx?.get(computeContextKey);

    // 3. Strip THIS context from args (reference equality — safe)
    const argsWithoutCtx = asyncCtx?.stripFromArgs(args) ?? args;

    // 4. Build string key and check cache
    const key = this.buildKey(instance, argsWithoutCtx);
    const existing = ComputedRegistry.get(key);
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
      const cached = ComputedRegistry.get(key);
      if (cached?.isConsistent) return cached;

      // 6. Create new Computed + ComputeContext
      let newComputed: Computed<unknown>;
      const renewer = prevComputed?._renewer ?? (() => this.invoke(instance, argsWithoutCtx, newComputed));
      newComputed = new Computed<unknown>(key, renewer);

      const childComputeCtx = new ComputeContext(newComputed);
      const childAsyncCtx = (asyncCtx ?? AsyncContext.empty)
        .with(computeContextKey, childComputeCtx);

      // 7. Run with clean args — no stale AsyncContext to override .run()
      let fnResult: Result<unknown>;
      try {
        const value = childAsyncCtx.run(() => this._impl.call(instance, ...argsWithoutCtx));
        const resolved = value instanceof Promise ? await value : value;
        fnResult = result(resolved);
      } catch (e) {
        fnResult = errorResult(e);
      }

      newComputed.setOutput(fnResult);

      return newComputed;
    });

    // Register dependency from caller to the produced computed
    callerComputeCtx?.captureDependency(computed);
    return computed;
  }
}
