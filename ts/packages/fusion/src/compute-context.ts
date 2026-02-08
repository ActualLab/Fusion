import { AsyncContext, AsyncContextKey } from "@actuallab/core";
import type { Computed } from "./computed.js";

export const computeContextKey =
  new AsyncContextKey<ComputeContext | undefined>("ComputeContext", undefined);

/** Tracks the currently executing computation and captures dependencies. */
export class ComputeContext {
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

  /** Resolve ComputeContext from an AsyncContext (or current). */
  static from(ctx: AsyncContext | undefined): ComputeContext | undefined {
    return AsyncContext.from(ctx)?.get(computeContextKey);
  }
}
