import { type AsyncContext, type IResult, PromiseSource, resolvedVoidPromise, type Result } from "@actuallab/core";
import { type Computed, StateBoundComputed } from "./computed.js";

/** Base class for all reactive state types. */
export abstract class State<T> implements IResult<T> {
  protected _computed!: Computed<T>;
  protected _updateIndex = 0;
  protected _lastNonErrorValue: T | undefined;
  private _whenUpdatedSource: PromiseSource<void> | null = null;

  get updateIndex(): number {
    return this._updateIndex;
  }

  get lastNonErrorValue(): T | undefined {
    return this._lastNonErrorValue;
  }

  get hasValue(): boolean {
    return this._computed.hasValue;
  }

  get hasError(): boolean {
    return this._computed.hasError;
  }

  get value(): T {
    return this._computed.value;
  }

  get error(): unknown {
    return this._computed.error;
  }

  get valueOrUndefined(): T | undefined {
    return this._computed.valueOrUndefined;
  }

  get output(): Result<T> {
    return this._computed.output;
  }

  get computed(): Computed<T> {
    return this._computed;
  }

  use(asyncContext?: AsyncContext): T | Promise<T> {
    return this._computed.use(asyncContext);
  }

  useInconsistent(asyncContext?: AsyncContext): T {
    return this._computed.useInconsistent(asyncContext);
  }

  update(): Computed<T> | Promise<Computed<T>> {
    return this._computed.update();
  }

  recompute(): Computed<T> | Promise<Computed<T>> {
    this._computed.invalidate();
    return this.update();
  }

  whenInvalidated(): Promise<void> {
    return this._computed.whenInvalidated();
  }

  whenUpdated(): Promise<void> {
    return (this._whenUpdatedSource ??= new PromiseSource<void>()).promise;
  }

  whenFirstTimeUpdated(): Promise<void> {
    return this._updateIndex > 0 ? resolvedVoidPromise : this.whenUpdated();
  }

  protected _initialize(output: Result<T> | T, renewer?: () => Computed<T> | Promise<Computed<T>>): void {
    this._computed = new StateBoundComputed<T>(this, renewer);
    this._computed.setOutput(output);
    this._lastNonErrorValue = this._computed.valueOrUndefined;
  }

  protected _update(computed: Computed<T>, output: Result<T> | T): void {
    computed.setOutput(output);
    this._computed = computed;
    if (computed.hasValue)
      this._lastNonErrorValue = computed.value;
    this._updateIndex++;
    this._whenUpdatedSource?.resolve(undefined);
    this._whenUpdatedSource = null;
  }
}
