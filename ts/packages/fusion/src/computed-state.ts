import { AsyncContext, PromiseSource, type Result, result, errorResult } from "@actuallab/core";
import { type Computed, StateBoundComputed } from "./computed.js";
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
  private _cancelDelaySource = new PromiseSource<void>();
  private _renewer: () => Computed<T> | Promise<Computed<T>>;
  private _hasOutput = false;

  constructor(computer: StateComputer<T>, options?: ComputedStateOptions<T>) {
    super();
    this._computer = computer;
    this._updateDelayer = options?.updateDelayer ?? defaultUpdateDelayer;
    this._disposeController = new AbortController();
    this._renewer = async () => {
      const whenUpdated = this.whenUpdated();
      this._cancelDelaySource.resolve(undefined);
      await whenUpdated;
      return this._computed;
    };

    // Create initial computed — _updateCycle will replace it on first iteration
    if (options?.initialOutput !== undefined || options?.initialValue !== undefined) {
      this._initialize(options.initialOutput ?? options.initialValue as T, this._renewer);
      this._hasOutput = true;
    }
    // else: _computed stays unset until _updateCycle calls _update()

    void this._updateCycle();
  }

  override get hasValue(): boolean {
    if (!this._hasOutput && this._updateIndex === 0) return false;
    return this._computed.hasValue;
  }

  override get hasError(): boolean {
    if (!this._hasOutput && this._updateIndex === 0) return false;
    return this._computed.hasError;
  }

  override get value(): T {
    if (!this._hasOutput && this._updateIndex === 0) throw new Error("ComputedState has no value yet.");
    if (this._computed.hasValue) return this._computed.value;
    if (this._lastNonErrorValue !== undefined) return this._lastNonErrorValue;
    throw new Error("ComputedState has no value yet.");
  }

  override get error(): unknown {
    if (!this._hasOutput && this._updateIndex === 0) return undefined;
    return this._computed.error;
  }

  override get valueOrUndefined(): T | undefined {
    if (!this._hasOutput && this._updateIndex === 0) return undefined;
    return this._computed.valueOrUndefined ?? this._lastNonErrorValue;
  }

  override get output(): Result<T> {
    if (!this._hasOutput && this._updateIndex === 0) throw new Error("ComputedState has no output yet.");
    return this._computed.output;
  }

  override get computed(): Computed<T> {
    if (!this._hasOutput && this._updateIndex === 0) throw new Error("ComputedState has not been computed yet.");
    return this._computed;
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
        const computed = new StateBoundComputed<T>(this, this._renewer);
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
        if (this._cancelDelaySource.isCompleted)
          this._cancelDelaySource = new PromiseSource<void>();

        // Wait for invalidation (or cancellation via dispose)
        try {
          await computed.whenInvalidated(disposeSignal);
        } catch {
          return; // Cancelled via dispose
        }

        // Wait for delay (cancellable by renewer)
        await Promise.race([
          this._updateDelayer(disposeSignal),
          this._cancelDelaySource.promise,
        ]);
      }

    } catch (e) {
      // Intended, do nothing
    }
  }
}
