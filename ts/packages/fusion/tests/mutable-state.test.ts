import { describe, it, expect, beforeEach } from "vitest";
import { AsyncContext } from "@actuallab/core";
import { MutableState } from "../src/index.js";

describe("MutableState", () => {
  beforeEach(() => {
    AsyncContext.current = undefined;
  });

  it("should hold an initial value", () => {
    const state = new MutableState(42);
    expect(state.value).toBe(42);
  });

  it("should update value on set", () => {
    const state = new MutableState(1);
    state.set(2);
    expect(state.value).toBe(2);
  });

  it("should resolve whenUpdated on set", async () => {
    const state = new MutableState(1);
    const updated = state.whenUpdated();
    state.set(10);
    await updated;
    expect(state.value).toBe(10);
    expect(state.updateIndex).toBe(1);
  });

  it("should invalidate old computed on set", () => {
    const state = new MutableState(1);
    const oldComputed = state.computed;
    state.set(2);
    expect(oldComputed.isConsistent).toBe(false);
  });

  it("should implement State interface", () => {
    const state = new MutableState(42);
    expect(state.value).toBe(42);
    expect(state.computed).toBeDefined();
  });

  it("should expose IResult properties", () => {
    const state = new MutableState(42);
    expect(state.hasValue).toBe(true);
    expect(state.hasError).toBe(false);
    expect(state.value).toBe(42);
    expect(state.valueOrUndefined).toBe(42);
    expect(state.error).toBeUndefined();
  });
});
