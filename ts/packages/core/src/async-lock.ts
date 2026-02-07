/** Promise-based mutual exclusion lock for serializing async operations. */
export class AsyncLock {
  private _queue: (() => void)[] = [];
  private _locked = false;

  get isLocked(): boolean {
    return this._locked;
  }

  async acquire(): Promise<void> {
    if (!this._locked) {
      this._locked = true;
      return;
    }
    return new Promise<void>((resolve) => {
      this._queue.push(resolve);
    });
  }

  release(): void {
    if (!this._locked)
      throw new Error("AsyncLock is not locked.");

    const next = this._queue.shift();
    if (next !== undefined) {
      // Transfer lock to the next waiter
      next();
    } else {
      this._locked = false;
    }
  }

  async run<T>(fn: () => T | Promise<T>): Promise<T> {
    await this.acquire();
    try {
      return await fn();
    } finally {
      this.release();
    }
  }
}
