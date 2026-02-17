import { EventHandlerSet } from "@actuallab/core";

/** Singleton tracking active UI commands — mirrors .NET's UIActionTracker. */
export class UIActionTracker {
  private _activeCount = 0;
  readonly changed = new EventHandlerSet<void>();
  readonly errors: unknown[] = [];

  get isActive(): boolean {
    return this._activeCount > 0;
  }

  /** Run a command silently — errors are added to the error list but not thrown. */
  async run(fn: () => Promise<unknown>): Promise<void> {
    this._activeCount++;
    this.changed.trigger();
    try {
      await fn();
    } catch (e) {
      this.errors.push(e);
    } finally {
      this._activeCount--;
      // Buffer 50ms for invalidations to arrive before signaling completion
      await new Promise<void>(r => setTimeout(r, 50));
      this.changed.trigger();
    }
  }

  /** Call a command and return the result — errors are added to the error list AND thrown. */
  async call<T>(fn: () => Promise<T>): Promise<T> {
    this._activeCount++;
    this.changed.trigger();
    try {
      return await fn();
    } catch (e) {
      this.errors.push(e);
      throw e;
    } finally {
      this._activeCount--;
      await new Promise<void>(r => setTimeout(r, 50));
      this.changed.trigger();
    }
  }

  dismissError(index: number): void {
    if (index >= 0 && index < this.errors.length) {
      this.errors.splice(index, 1);
      this.changed.trigger();
    }
  }
}

export let uiActions = new UIActionTracker();
