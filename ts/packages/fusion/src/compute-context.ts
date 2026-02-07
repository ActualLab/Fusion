import { AsyncContextKey } from "@actuallab/core";
import type { Computed } from "./computed.js";
import { _setContextAccessor } from "./computed.js";

/** Tracks the currently executing computation and captures dependencies. */
export class ComputeContext {
  static current: ComputeContext | undefined = undefined;

  readonly computed: Computed<unknown>;
  private _isCapturing = true;

  constructor(computed: Computed<unknown>) {
    this.computed = computed;
  }

  get isCapturing(): boolean {
    return this._isCapturing;
  }

  stopCapturing(): void {
    this._isCapturing = false;
  }

  captureDependency(dependency: Computed<unknown>): void {
    if (!this._isCapturing) return;
    this.computed.addDependency(dependency);
  }

  static run<T>(context: ComputeContext | undefined, fn: () => T): T {
    const prev = ComputeContext.current;
    ComputeContext.current = context;
    try {
      return fn();
    } finally {
      ComputeContext.current = prev;
    }
  }
}

// Register the context accessor so Computed.use() can capture dependencies
// without a direct circular import.
_setContextAccessor(() => ComputeContext.current);

export const computeContextKey =
  new AsyncContextKey<ComputeContext | undefined>("ComputeContext", undefined);
