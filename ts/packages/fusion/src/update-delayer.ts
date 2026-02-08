/** Controls when state re-computation happens after invalidation. */
export type UpdateDelayer = (abortSignal?: AbortSignal) => Promise<void>;

/** Re-computes after a fixed delay in milliseconds. */
export class FixedDelayer {
  static readonly zero: UpdateDelayer = () => Promise.resolve();

  private static _cache = new Map<number, UpdateDelayer>();

  static get(ms: number): UpdateDelayer {
    if (ms <= 0) return FixedDelayer.zero;
    let delayer = FixedDelayer._cache.get(ms);
    if (delayer === undefined) {
      delayer = new FixedDelayer(ms).delay;
      FixedDelayer._cache.set(ms, delayer);
    }
    return delayer;
  }

  readonly ms: number;
  readonly delay: UpdateDelayer;

  constructor(ms: number) {
    this.ms = ms;
    this.delay = ms > 10_000
      ? (abortSignal?: AbortSignal) => new Promise<void>((resolve, reject) => {
          const timer = setTimeout(resolve, this.ms);
          if (abortSignal === undefined) return;
          if (abortSignal.aborted) { clearTimeout(timer); reject(abortSignal.reason); return; }
          abortSignal.addEventListener("abort", () => {
            clearTimeout(timer);
            reject(abortSignal.reason);
          }, { once: true });
        })
      : () => new Promise<void>((resolve) => setTimeout(resolve, this.ms));
  }
}

export let defaultUpdateDelayer: UpdateDelayer = FixedDelayer.zero;
