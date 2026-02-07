import { describe, it, expect, beforeEach } from "vitest";
import {
  ComputedState,
  ComputeFunction,
  ComputeContext,
  computedRegistry,
  FixedDelayer,
  NoDelayer,
} from "../src/index.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

describe("ComputedState", () => {
  beforeEach(() => {
    computedRegistry.clear();
    ComputeContext.current = undefined;
  });

  it("should compute initial value", async () => {
    const fn = new ComputeFunction("Svc", "get", () => 42);
    const state = new ComputedState<number>(() => fn.invoke([1]));

    await state.initialize();
    expect(state.value).toBe(42);
  });

  it("should auto-update on invalidation with NoDelayer", async () => {
    let counter = 0;
    const fn = new ComputeFunction("Svc", "get", () => ++counter);
    const state = new ComputedState<number>(() => fn.invoke([1]), new NoDelayer());

    await state.initialize();
    expect(state.value).toBe(1);

    // Invalidate the underlying computed
    state.computed?.invalidate();

    // Wait for auto-update
    await delay(20);
    expect(state.value).toBe(2);
  });

  it("should auto-update on invalidation with FixedDelayer", async () => {
    let counter = 0;
    const fn = new ComputeFunction("Svc", "get", () => ++counter);
    const state = new ComputedState<number>(() => fn.invoke([1]), new FixedDelayer(30));

    await state.initialize();
    expect(state.value).toBe(1);

    state.computed?.invalidate();

    // Should not have updated yet
    await delay(10);
    expect(state.value).toBe(1);

    // Wait for delay + update
    await delay(50);
    expect(state.value).toBe(2);
  });

  it("should fire invalidated and updated events", async () => {
    let counter = 0;
    const fn = new ComputeFunction("Svc", "get", () => ++counter);
    const state = new ComputedState<number>(() => fn.invoke([1]), new NoDelayer());

    let invalidatedCount = 0;
    let updatedCount = 0;
    state.invalidated.add(() => invalidatedCount++);
    state.updated.add(() => updatedCount++);

    await state.initialize();
    expect(updatedCount).toBe(1); // initial

    state.computed?.invalidate();
    await delay(20);

    expect(invalidatedCount).toBe(1);
    expect(updatedCount).toBe(2); // initial + re-compute
  });

  it("should track lastNonErrorValue", async () => {
    let counter = 0;
    const fn = new ComputeFunction("Svc", "get", () => {
      counter++;
      if (counter === 2) throw new Error("transient");
      return counter;
    });
    const state = new ComputedState<number>(() => fn.invoke([1]), new NoDelayer());

    await state.initialize();
    expect(state.value).toBe(1);
    expect(state.lastNonErrorValue).toBe(1);

    // Second call will throw
    state.computed?.invalidate();
    await delay(20);

    // Value is undefined because error, but lastNonErrorValue preserved
    expect(state.value).toBeUndefined();
    expect(state.lastNonErrorValue).toBe(1);
  });

  it("should not update after dispose", async () => {
    let counter = 0;
    const fn = new ComputeFunction("Svc", "get", () => ++counter);
    const state = new ComputedState<number>(() => fn.invoke([1]), new NoDelayer());

    await state.initialize();
    const computed = state.computed;

    state.dispose();
    expect(state.isDisposed).toBe(true);

    computed?.invalidate();
    await delay(20);

    expect(counter).toBe(1); // no recompute
  });
});
