import { describe, it, expect, beforeEach } from "vitest";
import { MutableState, ComputeContext, computedRegistry } from "../src/index.js";

describe("MutableState", () => {
  beforeEach(() => {
    computedRegistry.clear();
    ComputeContext.current = undefined;
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

  it("should fire changed event on set", () => {
    const state = new MutableState(1);
    const values: number[] = [];
    state.changed.add((v) => values.push(v));
    state.set(10);
    state.set(20);
    expect(values).toEqual([10, 20]);
  });

  it("should invalidate old computed on set", () => {
    const state = new MutableState(1);
    const oldComputed = state.computed;
    state.set(2);
    expect(oldComputed.isConsistent).toBe(false);
  });
});
