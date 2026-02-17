// .NET counterparts: ActualLab.Net.RetryDelayer, ActualLab.Net.RetryDelay

import { EventHandlerSet } from "./events.js";
import { RetryDelaySeq } from "./retry-delay-seq.js";

export interface RetryDelay {
  readonly promise: Promise<void>;
  readonly endsAt: number;       // Date.now()-based timestamp, 0 = no delay
  readonly isLimitExceeded: boolean;
}

export const RetryDelayNone: RetryDelay = {
  promise: Promise.resolve(),
  endsAt: 0,
  isLimitExceeded: false,
};

export const RetryDelayLimitExceeded: RetryDelay = {
  promise: Promise.resolve(),
  endsAt: 0,
  isLimitExceeded: true,
};

export class RetryDelayer {
  private _cancelController = new AbortController();

  delays: RetryDelaySeq = RetryDelaySeq.fixed(1000);
  limit: number | undefined = undefined;

  readonly cancelDelaysChanged = new EventHandlerSet<void>();

  getDelay(tryIndex: number, cancellationSignal?: AbortSignal): RetryDelay {
    if (this.limit !== undefined && tryIndex >= this.limit)
      return RetryDelayLimitExceeded;

    const delayMs = this.delays.getDelay(tryIndex);
    if (tryIndex === 0 || delayMs <= 0)
      return RetryDelayNone;

    const actualDelayMs = Math.max(1, delayMs);
    const endsAt = Date.now() + actualDelayMs;

    const cancelSignal = this._cancelController.signal;
    const promise = new Promise<void>((resolve, reject) => {
      const timer = setTimeout(() => {
        cleanup();
        resolve();
      }, actualDelayMs);

      const onCancel = () => {
        clearTimeout(timer);
        cleanup();
        resolve(); // cancelDelays resolves, does NOT reject
      };

      const onAbort = () => {
        clearTimeout(timer);
        cleanup();
        reject(new Error("Retry delay aborted."));
      };

      const cleanup = () => {
        cancelSignal.removeEventListener("abort", onCancel);
        cancellationSignal?.removeEventListener("abort", onAbort);
      };

      cancelSignal.addEventListener("abort", onCancel, { once: true });
      cancellationSignal?.addEventListener("abort", onAbort, { once: true });
    });

    return { promise, endsAt, isLimitExceeded: false };
  }

  cancelDelays(): void {
    const old = this._cancelController;
    this._cancelController = new AbortController();
    old.abort();
    this.cancelDelaysChanged.trigger();
  }
}
