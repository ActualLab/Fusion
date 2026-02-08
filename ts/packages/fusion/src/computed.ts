import { type Result, ok, error } from "@actuallab/core";
import type { ComputedInput } from "./computed-input.js";
import { computedRegistry } from "./computed-registry.js";

let _nextVersion = 0;

export const enum ConsistencyState {
  Computing = 0,
  Consistent = 1,
  Invalidated = 2,
}

// Resolved lazily to break the Computed ↔ ComputeContext circular import.
// Set by compute-context.ts on module load.
let _getCurrentContext: (() => { captureDependency(dep: Computed<unknown>): void } | undefined) | undefined;

export function _setContextAccessor(fn: typeof _getCurrentContext): void {
  _getCurrentContext = fn;
}

/** Core Fusion abstraction — a cached computation with dependency tracking and invalidation. */
export class Computed<T> {
  readonly input: ComputedInput;
  private _version: number;
  private _state: ConsistencyState;
  private _output: Result<T> | undefined;
  private _dependencies = new Set<Computed<unknown>>();
  private _dependants = new Map<string, { input: ComputedInput; version: number }>();
  private _onInvalidated: (() => void) | undefined;

  constructor(input: ComputedInput) {
    this.input = input;
    this._version = ++_nextVersion;
    this._state = ConsistencyState.Computing;
  }

  get version(): number {
    return this._version;
  }

  get state(): ConsistencyState {
    return this._state;
  }

  get output(): Result<T> | undefined {
    return this._output;
  }

  get isConsistent(): boolean {
    return this._state === ConsistencyState.Consistent;
  }

  get value(): T {
    if (this._output === undefined) throw new Error("Computed has no value yet.");
    if (!this._output.ok) throw this._output.error;
    return this._output.value;
  }

  get dependencies(): ReadonlySet<Computed<unknown>> {
    return this._dependencies;
  }

  set onInvalidated(handler: (() => void) | undefined) {
    this._onInvalidated = handler;
  }

  use(): T {
    // Register as dependency of the currently executing computation
    const ctx = _getCurrentContext?.();
    if (ctx !== undefined) ctx.captureDependency(this as Computed<unknown>);

    if (this._output === undefined) throw new Error("Computed has no value yet.");
    if (!this._output.ok) throw this._output.error;
    return this._output.value;
  }

  setOutput(value: T): void {
    if (this._state !== ConsistencyState.Computing)
      throw new Error("Cannot set output on a non-computing Computed.");
    this._output = ok(value);
    this._state = ConsistencyState.Consistent;
  }

  setError(err: unknown): void {
    if (this._state !== ConsistencyState.Computing)
      throw new Error("Cannot set error on a non-computing Computed.");
    this._output = error(err);
    this._state = ConsistencyState.Consistent;
  }

  invalidate(): void {
    if (this._state === ConsistencyState.Invalidated) return;
    this._state = ConsistencyState.Invalidated;

    // Clear forward references
    this._dependencies.clear();

    // Notify dependants via weak-like backward references
    for (const [, entry] of this._dependants) {
      const dependant = computedRegistry.get(entry.input);
      if (dependant != null && dependant.version === entry.version)
        dependant.invalidate();
    }
    this._dependants.clear();

    this._onInvalidated?.();
  }

  addDependency(dependency: Computed<unknown>): void {
    this._dependencies.add(dependency);
    dependency._dependants.set(this.input.key, {
      input: this.input,
      version: this._version,
    });
  }
}
