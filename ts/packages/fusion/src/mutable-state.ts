import { EventHandlerSet } from "@actuallab/core";
import { Computed } from "./computed.js";
import { ComputedInput } from "./computed-input.js";
import { computedRegistry } from "./computed-registry.js";

let _mutableStateCounter = 0;

/** Manually-settable reactive state that can participate in the Fusion dependency graph. */
export class MutableState<T> {
  private _computed: Computed<T>;
  private _stateId: string;

  readonly changed = new EventHandlerSet<T>();

  constructor(initialValue: T) {
    this._stateId = `MutableState#${++_mutableStateCounter}`;
    const input = new ComputedInput(this._stateId, "value", []);
    this._computed = new Computed<T>(input);
    this._computed.setOutput(initialValue);
    computedRegistry.register(this._computed);
  }

  get value(): T {
    return this._computed.use();
  }

  get computed(): Computed<T> {
    return this._computed;
  }

  set(value: T): void {
    // Invalidate the old computed
    this._computed.invalidate();

    // Create a new computed with the new value
    const input = new ComputedInput(this._stateId, "value", []);
    this._computed = new Computed<T>(input);
    this._computed.setOutput(value);
    computedRegistry.register(this._computed);

    this.changed.trigger(value);
  }
}
