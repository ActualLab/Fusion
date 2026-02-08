import { type AsyncContext, Result } from "@actuallab/core";
import { StateBoundComputed } from "./computed.js";
import { State } from "./state.js";

/** Manually-settable reactive state that can participate in the Fusion dependency graph. */
export class MutableState<T> extends State<T> {
  constructor(initialOutput: Result<T> | T) {
    super();
    this._initialize(initialOutput);
  }

  override use(asyncContext?: AsyncContext): T {
    return this._computed.use(asyncContext) as T;
  }

  set(output: Result<T> | T): void {
    this._computed.invalidate();
    this._update(new StateBoundComputed<T>(this), output);
  }
}
