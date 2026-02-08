import { AsyncContext, type Result, result, errorResult } from "@actuallab/core";
import { StateBoundComputed } from "./computed.js";
import { ComputeContext, computeContextKey } from "./compute-context.js";
import { defaultUpdateDelayer, type UpdateDelayer } from "./update-delayer.js";
import { State } from "./state.js";

export type StateComputer<T> = () => T | Promise<T>;

/** Constructor options for ComputedState. */
export interface ComputedStateOptions<T> {
  initialValue?: T;
  initialOutput?: Result<T>;
  updateDelayer?: UpdateDelayer;
}

/** Auto-updating reactive state wrapper — re-computes on invalidation with configurable delay. */
export class ComputedState<T> extends State<T> {
  private _computer: StateComputer<T>;
  private _updateDelayer: UpdateDelayer;
  private _disposeController: AbortController;

  constructor(computer: StateComputer<T>, options?: ComputedStateOptions<T>) {
    super();
    this._computer = computer;
    this._updateDelayer = options?.updateDelayer ?? defaultUpdateDelayer;
    this._disposeController = new AbortController();

    // Create initial computed
    if (options?.initialOutput !== undefined || options?.initialValue !== undefined)
      this._initialize(options.initialOutput ?? options.initialValue as T);
    else
      this._computed = new StateBoundComputed<T>(this);

    void this._updateCycle();
  }

  override get value(): T {
    if (this._computed.hasValue) return this._computed.value;
    if (this._lastNonErrorValue !== undefined) return this._lastNonErrorValue;
    throw new Error("ComputedState has no value yet.");
  }

  override get valueOrUndefined(): T | undefined {
    return this._computed.valueOrUndefined ?? this._lastNonErrorValue;
  }

  get isDisposed(): boolean {
    return this._disposeController.signal.aborted;
  }

  dispose(): void {
    if (!this.isDisposed)
      this._disposeController.abort();
  }

  private async _updateCycle(): Promise<void> {
    const disposeSignal = this._disposeController.signal;
    try {
      while (!disposeSignal.aborted) {
        // Compute
        const computed = new StateBoundComputed<T>(this);
        const computeCtx = new ComputeContext(computed as StateBoundComputed<unknown>);
        const asyncCtx = (AsyncContext.current ?? AsyncContext.empty)
          .with(computeContextKey, computeCtx);

        let output: Result<T>;
        try {
          const value = asyncCtx.run(() => this._computer());
          const resolved = value instanceof Promise ? await value : value;
          output = result(resolved);
        } catch (e) {
          output = errorResult(e);
        }
        this._update(computed, output);

        // Wait for invalidation (or cancellation via dispose)
        try {
          await computed.whenInvalidated(disposeSignal);
        } catch {
          return; // Cancelled via dispose
        }

        await this._updateDelayer(disposeSignal);
      }

    } catch (e) {
      // Intended, do nothing
    } 
  }
}
