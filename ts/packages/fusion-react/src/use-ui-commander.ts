import { createContext, useContext } from "react";
import { UIActionTracker, uiActions } from "@actuallab/fusion";

/** React context for UIActionTracker. */
export const UIActionTrackerContext = createContext<UIActionTracker>(uiActions);

/** Returns the UIActionTracker from context — use its run/call methods to wrap async commands. */
export function useUIActionTracker(): UIActionTracker {
  return useContext(UIActionTrackerContext);
}
