/** Externally-resolvable promise — similar to .NET's TaskCompletionSource<T>. */
export class PromiseSource<T> {
  readonly promise: Promise<T>;
  private _resolve!: (value: T | PromiseLike<T>) => void;
  private _reject!: (reason?: unknown) => void;
  private _isCompleted = false;

  constructor() {
    this.promise = new Promise<T>((resolve, reject) => {
      this._resolve = resolve;
      this._reject = reject;
    });
  }

  get isCompleted(): boolean {
    return this._isCompleted;
  }

  resolve(value: T): boolean {
    if (this._isCompleted) return false;
    this._isCompleted = true;
    this._resolve(value);
    return true;
  }

  reject(reason?: unknown): boolean {
    if (this._isCompleted) return false;
    this._isCompleted = true;
    this._reject(reason);
    return true;
  }
}
