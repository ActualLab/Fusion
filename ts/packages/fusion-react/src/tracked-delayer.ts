import type { UpdateDelayer } from "@actuallab/fusion";
import type { UIActionTracker } from "./ui-action-tracker.js";

/**
 * Creates an UpdateDelayer that skips delay when the tracker is active,
 * otherwise waits for delayMs. Mirrors .NET's UpdateDelayer + UIActionTracker pattern.
 */
export function createTrackedDelayer(tracker: UIActionTracker, delayMs: number): UpdateDelayer {
  return (abortSignal?: AbortSignal) => {
    if (tracker.isActive)
      return Promise.resolve();

    return new Promise<void>((resolve, reject) => {
      const timer = setTimeout(resolve, delayMs);

      // Cancel delay when tracker becomes active
      const onChanged = () => {
        if (tracker.isActive) {
          clearTimeout(timer);
          tracker.changed.remove(onChanged);
          resolve();
        }
      };
      tracker.changed.add(onChanged);

      if (abortSignal !== undefined) {
        if (abortSignal.aborted) {
          clearTimeout(timer);
          tracker.changed.remove(onChanged);
          reject(abortSignal.reason);
          return;
        }
        abortSignal.addEventListener("abort", () => {
          clearTimeout(timer);
          tracker.changed.remove(onChanged);
          reject(abortSignal.reason);
        }, { once: true });
      }
    });
  };
}
