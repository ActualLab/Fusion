import { describe, it, expect, beforeEach } from "vitest";
import { AsyncContext } from "@actuallab/core";
import { MutableState, StateBase, computedRegistry } from "../src/index.js";

describe("MutableState", () => {
  beforeEach(() => {
    computedRegistry.clear();
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

  it("should extend StateBase", () => {
    const state = new MutableState(42);
    expect(state).toBeInstanceOf(StateBase);
    expect(state.stateKey).toMatch(/^MutableState#\d+$/);
  });

  it("should expose output and error", () => {
    const state = new MutableState(42);
    expect(state.output?.ok).toBe(true);
    if (state.output?.ok) {
      expect(state.output.value).toBe(42);
    }
    expect(state.error).toBeUndefined();
  });
});
