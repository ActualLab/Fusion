import { describe, it, expect } from "vitest";
import {
  abortSignalKey,
  AsyncContext,
} from "../src/index.js";

describe("abortSignalKey", () => {
  it("should carry AbortSignal through context", () => {
    const controller = new AbortController();
    const ctx = new AsyncContext().with(abortSignalKey, controller.signal);
    expect(ctx.get(abortSignalKey)).toBe(controller.signal);
  });

  it("should default to undefined", () => {
    const ctx = new AsyncContext();
    expect(ctx.get(abortSignalKey)).toBeUndefined();
  });
});
