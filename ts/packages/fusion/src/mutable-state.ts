import { EventHandlerSet, type Result, ok } from "@actuallab/core";
import { Computed } from "./computed.js";
import { ComputedInput } from "./computed-input.js";
import type { State } from "./state.js";

let _mutableStateCounter = 0;

/** Manually-settable reactive state that can participate in the Fusion dependency graph. */
export class MutableState<T> extends ComputedInput implements State<T> {
  private _computed: Computed<T>;

  readonly changed = new EventHandlerSet<T>();

  constructor(initialValue: T) {
    super(`MutableState#${++_mutableStateCounter}:value`);
    this._computed = new Computed<T>(this);
    this._computed.setOutput(initialValue);
  }

  get value(): T {
    return this._computed.use();
  }

  get error(): unknown {
    const output = this._computed.output;
    return output !== undefined && !output.ok ? output.error : undefined;
  }

  get output(): Result<T> | undefined {
    return this._computed.output;
  }

  get computed(): Computed<T> {
    return this._computed;
  }

  set(value: T): void {
    // Invalidate the old computed
    this._computed.invalidate();

    // Create a new computed with the new value (reuse this as input)
    this._computed = new Computed<T>(this);
    this._computed.setOutput(value);

    this.changed.trigger(value);
  }
}
