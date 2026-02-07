import { EventHandlerSet, type Result, error } from "@actuallab/core";
import type { Computed } from "./computed.js";
import type { UpdateDelayer } from "./update-delayer.js";
import { NoDelayer } from "./update-delayer.js";

export type StateComputer<T> = () => Promise<Computed<T>>;

/** Auto-updating reactive state wrapper — re-computes on invalidation with configurable delay. */
export class ComputedState<T> {
  private _computer: StateComputer<T>;
  private _delayer: UpdateDelayer;
  private _computed: Computed<T> | undefined;
  private _lastNonErrorValue: T | undefined;
  private _disposed = false;
  private _updateScheduled = false;

  readonly invalidated = new EventHandlerSet<void>();
  readonly updated = new EventHandlerSet<Result<T>>();

  constructor(computer: StateComputer<T>, delayer?: UpdateDelayer) {
    this._computer = computer;
    this._delayer = delayer ?? new NoDelayer();
  }

  get value(): T | undefined {
    return this._computed?.output?.ok === true ? this._computed.output.value : undefined;
  }

  get lastNonErrorValue(): T | undefined {
    return this._lastNonErrorValue;
  }

  get output(): Result<T> | undefined {
    return this._computed?.output;
  }

  get computed(): Computed<T> | undefined {
    return this._computed;
  }

  get isDisposed(): boolean {
    return this._disposed;
  }

  async initialize(): Promise<void> {
    await this._update();
  }

  dispose(): void {
    this._disposed = true;
    this._computed = undefined;
  }

  private async _update(): Promise<void> {
    if (this._disposed) return;

    try {
      const computed = await this._computer();
      this._computed = computed;

      if (computed.output?.ok === true) {
        this._lastNonErrorValue = computed.output.value;
      }

      this.updated.trigger(computed.output ?? error(new Error("No output")));

      // Subscribe to invalidation
      computed.onInvalidated = () => {
        if (this._disposed) return;
        this.invalidated.trigger();
        this._scheduleUpdate();
      };
    } catch (e) {
      const result: Result<T> = error(e);
      this.updated.trigger(result);
    }
  }

  private _scheduleUpdate(): void {
    if (this._updateScheduled || this._disposed) return;
    this._updateScheduled = true;

    void (async () => {
      try {
        await this._delayer.delay();
        this._updateScheduled = false;
        await this._update();
      } catch {
        this._updateScheduled = false;
      }
    })();
  }
}
