import type { Disposable } from "./disposable.js";
import { AsyncContext, AsyncContextKey } from "./async-context.js";

/** Lightweight cancellation token — similar to .NET's CancellationToken, not AbortController. */
export class CancellationToken {
  static readonly none = new CancellationToken(false);
  static readonly cancelled = new CancellationToken(true);

  /** Resolve CancellationToken from an AsyncContext (or current). */
  static from(ctx: AsyncContext | undefined): CancellationToken {
    return AsyncContext.from(ctx)?.get(cancellationTokenKey) ?? CancellationToken.none;
  }

  private _isCancelled: boolean;
  private _callbacks: (() => void)[] | null;

  constructor(cancelled: boolean) {
    this._isCancelled = cancelled;
    this._callbacks = cancelled ? null : [];
  }

  get isCancelled(): boolean {
    return this._isCancelled;
  }

  throwIfCancelled(): void {
    if (this._isCancelled) throw new CancellationError();
  }

  onCancelled(callback: () => void): Disposable {
    if (this._isCancelled) {
      callback();
      return { dispose: () => {} };
    }
    if (this._callbacks === null) return { dispose: () => {} };
    this._callbacks.push(callback);
    return {
      dispose: () => {
        if (this._callbacks === null) return;
        const idx = this._callbacks.indexOf(callback);
        if (idx >= 0) this._callbacks.splice(idx, 1);
      },
    };
  }

  toAbortSignal(): AbortSignal {
    if (this._isCancelled) return AbortSignal.abort();
    const controller = new AbortController();
    this.onCancelled(() => controller.abort());
    return controller.signal;
  }

  // Called by CancellationTokenSource
  _cancel(): void {
    if (this._isCancelled) return;
    this._isCancelled = true;
    const callbacks = this._callbacks;
    this._callbacks = null;
    if (callbacks !== null) {
      for (const cb of callbacks) cb();
    }
  }
}

/** Creates CancellationTokens that can be cancelled — similar to .NET's CancellationTokenSource. */
export class CancellationTokenSource implements Disposable {
  readonly token: CancellationToken;
  private _disposed = false;

  constructor() {
    this.token = new CancellationToken(false);
  }

  cancel(): void {
    if (this._disposed) return;
    this.token._cancel();
  }

  dispose(): void {
    if (this._disposed) return;
    this._disposed = true;
  }
}

/** Error thrown when a CancellationToken is cancelled. */
export class CancellationError extends Error {
  constructor() {
    super("Operation was cancelled.");
    this.name = "CancellationError";
  }
}

export const cancellationTokenKey =
  new AsyncContextKey<CancellationToken>("CancellationToken", CancellationToken.none);
