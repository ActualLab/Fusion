import type { Result } from "@actuallab/core";
import type { Computed } from "./computed.js";
import type { UpdateDelayer } from "./update-delayer.js";

/** Common interface for all reactive state types. */
export interface State<T> {
  readonly value: T;
  readonly error: unknown;
  readonly output: Result<T> | undefined;
  readonly computed: Computed<T> | undefined;
}

/** Constructor options for state types. */
export interface StateOptions<T> {
  initialValue?: T;
  initialOutput?: Result<T>;
  delayer?: UpdateDelayer;
}
