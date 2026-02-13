import { createContext, useContext } from "react";
import { UIActionTracker } from "./ui-action-tracker.js";

/** Global default UIActionTracker instance. */
const defaultTracker = new UIActionTracker();

/** React context for UIActionTracker. */
export const UIActionTrackerContext = createContext<UIActionTracker>(defaultTracker);

/** Returns a commander that wraps async commands with UIActionTracker. */
export function useUICommander(): {
  run: (fn: () => Promise<unknown>) => Promise<void>;
  call: <T>(fn: () => Promise<T>) => Promise<T>;
} {
  const tracker = useContext(UIActionTrackerContext);
  return {
    run: (fn: () => Promise<unknown>) => tracker.run(fn),
    call: <T>(fn: () => Promise<T>) => tracker.call(fn),
  };
}
