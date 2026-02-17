import { uiActions } from "./ui-action-tracker.js";
import { FixedDelayer, type UpdateDelayer } from "./update-delayer.js";

/** Re-computes after a fixed delay, but skips the delay when UIActionTracker is active. */
export class UIUpdateDelayer {
  private static _cache = new Map<number, UpdateDelayer>();

  static get(ms: number): UpdateDelayer {
    if (ms <= 0) return FixedDelayer.zero;
    let delayer = UIUpdateDelayer._cache.get(ms);
    if (delayer !== undefined) return delayer;

    const t = uiActions;
    delayer = (abortSignal?: AbortSignal) => {
      if (t.isActive)
        return Promise.resolve();

      return new Promise<void>((resolve, reject) => {
        const timer = setTimeout(resolve, ms);

        // Cancel delay when tracker becomes active
        const onChanged = () => {
          if (t.isActive) {
            clearTimeout(timer);
            t.changed.remove(onChanged);
            resolve();
          }
        };
        t.changed.add(onChanged);

        if (abortSignal !== undefined) {
          if (abortSignal.aborted) {
            clearTimeout(timer);
            t.changed.remove(onChanged);
            reject(abortSignal.reason);
            return;
          }
          abortSignal.addEventListener("abort", () => {
            clearTimeout(timer);
            t.changed.remove(onChanged);
            reject(abortSignal.reason);
          }, { once: true });
        }
      });
    };
    UIUpdateDelayer._cache.set(ms, delayer);
    return delayer;
  }
}
