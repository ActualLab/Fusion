import { AsyncLock, type Result, ok, error } from "@actuallab/core";
import { Computed } from "./computed.js";
import { ComputedInput } from "./computed-input.js";
import { ComputeContext } from "./compute-context.js";
import { computedRegistry } from "./computed-registry.js";

export type ComputeFn = (...args: unknown[]) => unknown;

/** Produces Computed<T> from a function — handles cache lookup, locking, dependency capture. */
export class ComputeFunction {
  readonly serviceId: string;
  readonly methodName: string;
  private _fn: ComputeFn;
  private _locks = new Map<string, AsyncLock>();

  constructor(serviceId: string, methodName: string, fn: ComputeFn) {
    this.serviceId = serviceId;
    this.methodName = methodName;
    this._fn = fn;
  }

  async invoke(args: unknown[]): Promise<Computed<unknown>> {
    const input = new ComputedInput(this.serviceId, this.methodName, args);

    // Capture the calling context BEFORE any await — this is the key
    // synchronous dependency capture point (see plan: F2)
    const callerCtx = ComputeContext.current;

    // Check cache first
    const existing = computedRegistry.get(input);
    if (existing != null && existing.isConsistent) {
      // Register dependency from caller to cached computed
      callerCtx?.captureDependency(existing);
      return existing;
    }

    // Lock per input to prevent duplicate computations
    let lock = this._locks.get(input.key);
    if (lock === undefined) {
      lock = new AsyncLock();
      this._locks.set(input.key, lock);
    }

    const computed = await lock.run(async () => {
      // Double-check after acquiring lock
      const cached = computedRegistry.get(input);
      if (cached != null && cached.isConsistent) return cached;

      const newComputed = new Computed<unknown>(input);
      computedRegistry.register(newComputed);

      // Run the function body with this computed's context
      const childCtx = new ComputeContext(newComputed);
      let result: Result<unknown>;
      try {
        const value = ComputeContext.run(childCtx, () => this._fn(...args));
        // Handle async functions — restore caller context around await
        const resolved = value instanceof Promise
          ? await wrapPromiseWithContext(value, callerCtx)
          : value;
        result = ok(resolved);
      } catch (e) {
        result = error(e);
      }

      if (result.ok) {
        newComputed.setOutput(result.value);
      } else {
        newComputed.setError(result.error);
      }

      return newComputed;
    });

    // Register dependency from caller to the produced computed
    callerCtx?.captureDependency(computed);
    return computed;
  }
}

// Wraps a promise to restore the given ComputeContext when it settles.
// This ensures the caller's context is active for dependency capture
// in continuations after awaiting inner compute calls.
function wrapPromiseWithContext<T>(
  promise: Promise<T>,
  ctx: ComputeContext | undefined,
): Promise<T> {
  return promise.then(
    (value) => {
      ComputeContext.current = ctx;
      return value;
    },
    (err) => {
      ComputeContext.current = ctx;
      throw err;
    },
  );
}
