import { describe, it, expect, beforeEach } from "vitest";
import { AsyncContext, errorResult } from "@actuallab/core";
import { ConsistencyState, MutableState } from "../src/index.js";

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

describe("MutableState.update()", () => {
  beforeEach(() => {
    AsyncContext.current = undefined;
  });

  it("should return current computed when consistent", () => {
    const state = new MutableState(42);
    const result = state.update();
    expect(result).toBe(state.computed);
  });

  it("should renew computed after invalidation", () => {
    const state = new MutableState(42);
    const oldComputed = state.computed;
    state.computed.invalidate();

    const renewed = state.update();
    // Renewer is synchronous, so result is a Computed, not a Promise
    expect(renewed).not.toBeInstanceOf(Promise);
    // New computed is consistent with the same value
    expect(renewed).toBe(state.computed);
    expect(state.computed.isConsistent).toBe(true);
    expect(state.value).toBe(42);
    // Old computed stays invalidated
    expect(oldComputed.isConsistent).toBe(false);
    // A new computed was created
    expect(state.computed).not.toBe(oldComputed);
  });

  it("should increment updateIndex on renew", () => {
    const state = new MutableState(42);
    expect(state.updateIndex).toBe(0);

    state.computed.invalidate();
    state.update();
    expect(state.updateIndex).toBe(1);
  });

  it("should preserve error output through renew", () => {
    const state = new MutableState(42);
    state.set(errorResult<number>(new Error("fail")));
    expect(state.hasError).toBe(true);

    state.computed.invalidate();
    state.update();

    expect(state.hasError).toBe(true);
    expect(() => state.value).toThrow("fail");
  });

  it("should work after multiple invalidate-update cycles", () => {
    const state = new MutableState(1);
    for (let i = 0; i < 5; i++) {
      state.computed.invalidate();
      state.update();
    }
    expect(state.updateIndex).toBe(5);
    expect(state.value).toBe(1);
    expect(state.computed.isConsistent).toBe(true);
  });

  it("should work after set followed by invalidate and update", () => {
    const state = new MutableState(1);
    state.set(99);
    expect(state.value).toBe(99);

    state.computed.invalidate();
    state.update();
    expect(state.value).toBe(99);
    expect(state.computed.isConsistent).toBe(true);
  });
});

describe("MutableState.recompute()", () => {
  beforeEach(() => {
    AsyncContext.current = undefined;
  });

  it("should invalidate and renew in one call", () => {
    const state = new MutableState(42);
    const oldComputed = state.computed;

    const renewed = state.recompute();
    expect(renewed).not.toBeInstanceOf(Promise);
    expect(oldComputed.isConsistent).toBe(false);
    expect(state.computed.isConsistent).toBe(true);
    expect(state.computed).not.toBe(oldComputed);
    expect(state.value).toBe(42);
  });

  it("should increment updateIndex", () => {
    const state = new MutableState("hello");
    expect(state.updateIndex).toBe(0);

    state.recompute();
    expect(state.updateIndex).toBe(1);
  });

  it("should be idempotent when called repeatedly", () => {
    const state = new MutableState(10);
    state.recompute();
    state.recompute();
    state.recompute();

    expect(state.updateIndex).toBe(3);
    expect(state.value).toBe(10);
    expect(state.computed.isConsistent).toBe(true);
  });

  it("should resolve whenUpdated promise", async () => {
    const state = new MutableState(42);
    const updated = state.whenUpdated();
    state.recompute();
    await updated;
    expect(state.updateIndex).toBe(1);
  });

  it("should preserve value after set then recompute", () => {
    const state = new MutableState(1);
    state.set(100);
    state.recompute();

    expect(state.value).toBe(100);
    expect(state.updateIndex).toBe(2); // 1 from set, 1 from recompute
  });
});

describe("MutableState renewer integration", () => {
  beforeEach(() => {
    AsyncContext.current = undefined;
  });

  it("should allow use() on invalidated computed to return renewed value", () => {
    const state = new MutableState(42);
    const oldComputed = state.computed;
    oldComputed.invalidate();

    // use() on the old computed should trigger renewer via update() -> _latest()
    const val = oldComputed.use();
    expect(val).toBe(42);
  });

  it("should keep computed chain working after set + invalidate", () => {
    const state = new MutableState("a");
    state.set("b");

    const computedAfterSet = state.computed;
    computedAfterSet.invalidate();

    const renewed = state.update();
    expect(renewed).not.toBeInstanceOf(Promise);
    expect(state.value).toBe("b");
    expect(state.computed.state).toBe(ConsistencyState.Consistent);
  });
});
