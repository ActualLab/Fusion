import { EventHandlerSet } from "@actuallab/core";

/** Singleton tracking active UI commands — mirrors .NET's UIActionTracker. */
export class UIActionTracker {
  private _activeCount = 0;
  readonly changed = new EventHandlerSet<void>();

  get isActive(): boolean {
    return this._activeCount > 0;
  }

  async run<T>(fn: () => Promise<T>): Promise<T> {
    this._activeCount++;
    this.changed.trigger();
    try {
      return await fn();
    } finally {
      this._activeCount--;
      // Buffer 50ms for invalidations to arrive before signaling completion
      await new Promise<void>(r => setTimeout(r, 50));
      this.changed.trigger();
    }
  }
}
