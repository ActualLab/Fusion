/** Synchronous dispose pattern, similar to .NET's IDisposable. */
export interface Disposable {
  dispose(): void;
}

/** Asynchronous dispose pattern, similar to .NET's IAsyncDisposable. */
export interface AsyncDisposable {
  disposeAsync(): Promise<void>;
}

/** Aggregates multiple disposables and disposes them together. */
export class DisposableBag implements Disposable, AsyncDisposable {
  private _items: (Disposable | AsyncDisposable)[] = [];
  private _disposed = false;

  get isDisposed(): boolean {
    return this._disposed;
  }

  add(item: Disposable | AsyncDisposable): void {
    if (this._disposed)
      throw new Error("DisposableBag is already disposed.");
    this._items.push(item);
  }

  dispose(): void {
    if (this._disposed) return;
    this._disposed = true;
    const items = this._items;
    this._items = [];
    // Dispose in reverse order (LIFO)
    for (let i = items.length - 1; i >= 0; i--) {
      const item = items[i];
      if (item != null && "dispose" in item) item.dispose();
    }
  }

  async disposeAsync(): Promise<void> {
    if (this._disposed) return;
    this._disposed = true;
    const items = this._items;
    this._items = [];
    for (let i = items.length - 1; i >= 0; i--) {
      const item = items[i];
      if (item == null) continue;
      if ("disposeAsync" in item) await item.disposeAsync();
      else if ("dispose" in item) item.dispose();
    }
  }
}
