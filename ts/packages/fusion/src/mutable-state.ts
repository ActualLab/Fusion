import { type AsyncContext, Result } from "@actuallab/core";
import { type Computed, StateBoundComputed } from "./computed.js";
import { State } from "./state.js";

/** Manually-settable reactive state that can participate in the Fusion dependency graph. */
export class MutableState<T> extends State<T> {
  private _renewer = (): Computed<T> => {
    const computed = new StateBoundComputed<T>(this, this._renewer);
    this._update(computed, this._computed.output);
    return computed;
  };

  constructor(initialOutput: Result<T> | T) {
    super();
    this._initialize(initialOutput, this._renewer);
  }

  override use(asyncContext?: AsyncContext): T {
    return this._computed.use(asyncContext) as T;
  }

  set(output: Result<T> | T): void {
    this._computed.invalidate();
    this._update(new StateBoundComputed<T>(this, this._renewer), output);
  }
}
