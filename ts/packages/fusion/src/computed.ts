import { AsyncContext, EventHandlerSet, type IResult, PromiseSource, Result, result } from "@actuallab/core";
import type { ComputedInput } from "./computed-input.js";
import { ComputedRegistry } from "./computed-registry.js";
import { ComputeContext, computeContextKey } from "./compute-context.js";

let _nextVersion = 0;

export const enum ConsistencyState {
  Computing = 0,
  Consistent = 1,
  Invalidated = 2,
}

/** Core Fusion abstraction — a cached computation with dependency tracking and invalidation. */
export class Computed<T> implements IResult<T> {
  readonly input: ComputedInput;
  private _version: number;
  private _state: ConsistencyState;
  private _output: Result<T> | undefined;
  private _dependencies = new Set<Computed<unknown>>();
  private _dependants = new Map<number, WeakRef<Computed<unknown>>>();
  readonly onInvalidated = new EventHandlerSet<void>();
  _renew: (() => Computed<T> | Promise<Computed<T>>) | undefined;

  constructor(input: ComputedInput) {
    this.input = input;
    this._version = ++_nextVersion;
    this._state = ConsistencyState.Computing;
  }

  get version(): number {
    return this._version;
  }

  get output(): Result<T> {
    if (this._output === undefined) throw new Error("Computed has no output yet.");
    return this._output;
  }

  get hasValue(): boolean {
    return this.output.hasValue;
  }

  get hasError(): boolean {
    return this.output.hasError;
  }

  get value(): T {
    return this.output.value;
  }

  get error(): unknown {
    return this.output.error;
  }

  get valueOrUndefined(): T | undefined {
    return this.output.valueOrUndefined;
  }

  get state(): ConsistencyState {
    return this._state;
  }

  get isConsistent(): boolean {
    return this._state === ConsistencyState.Consistent;
  }

  get dependencies(): ReadonlySet<Computed<unknown>> {
    return this._dependencies;
  }

  use(asyncContext?: AsyncContext): T | Promise<T> {
    const ctx = ComputeContext.from(asyncContext);

    if (this._state === ConsistencyState.Consistent) {
      ctx?.captureDependency(this as Computed<unknown>);
      return this.value;
    }

    if (this._renew !== undefined) {
      const renewed = this._renew();
      if (renewed instanceof Promise) {
        return renewed.then((c) => {
          ctx?.captureDependency(c as Computed<unknown>);
          return c.value;
        });
      }
      ctx?.captureDependency(renewed as Computed<unknown>);
      return renewed.value;
    }

    throw new Error("Cannot recompute: Computed is invalidated and has no _renew function.");
  }

  useInconsistent(asyncContext?: AsyncContext): T {
    ComputeContext.from(asyncContext)?.captureDependency(this as Computed<unknown>);
    return this.value;
  }

  setOutput(output: Result<T> | T): void {
    if (this._state !== ConsistencyState.Computing)
      throw new Error("Cannot set output on a non-computing Computed.");
    this._output = output instanceof Result ? output : result(output);
    this._state = ConsistencyState.Consistent;
    this._register();
  }

  protected _register(): void {
    ComputedRegistry.register(this as Computed<unknown>);
  }

  protected _unregister(): void {
    ComputedRegistry.unregister(this as Computed<unknown>);
  }

  invalidate(): void {
    if (this._state === ConsistencyState.Invalidated) return;
    this._state = ConsistencyState.Invalidated;

    // Clear forward references
    this._dependencies.clear();

    // Notify dependants via WeakRef backward references
    for (const [, ref] of this._dependants) {
      const dependant = ref.deref();
      if (dependant != null) dependant.invalidate();
    }
    this._dependants.clear();

    this.onInvalidated.trigger();
    this.onInvalidated.clear();

    this._unregister();
  }

  whenInvalidated(abortSignal?: AbortSignal): Promise<void> {
    if (this._state === ConsistencyState.Invalidated)
      return Promise.resolve();

    const ps = new PromiseSource<void>();
    const handler = () => ps.resolve(undefined);
    this.onInvalidated.add(handler);

    if (abortSignal !== undefined && !abortSignal.aborted) {
      abortSignal.addEventListener("abort", () => {
        this.onInvalidated.remove(handler);
        ps.reject(abortSignal.reason);
      }, { once: true });
    }

    return ps.promise;
  }

  addDependency(dependency: Computed<unknown>): void {
    this._dependencies.add(dependency);
    dependency._dependants.set(this._version, new WeakRef(this as Computed<unknown>));
  }
}

/** Computed variant for State types — skips registry registration since the State holds a direct reference. */
export class StateBoundComputed<T> extends Computed<T> {
  protected override _register(): void {
    // No registration — state holds direct reference
  }

  protected override _unregister(): void {
    // No unregistration — was never registered
  }
}
