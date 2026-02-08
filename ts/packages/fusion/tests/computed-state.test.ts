import { describe, it, expect, beforeEach } from "vitest";
import { AsyncContext } from "@actuallab/core";
import {
  ComputedState,
  FixedDelayer,
} from "../src/index.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

describe("ComputedState", () => {
  beforeEach(() => {
    AsyncContext.current = undefined;
  });

  it("should compute initial value", async () => {
    const state = new ComputedState<number>(() => 42);

    await state.whenFirstTimeUpdated();
    expect(state.updateIndex).toBe(1);
    expect(state.value).toBe(42);
  });

  it("should auto-update on invalidation with zero delayer", async () => {
    let counter = 0;
    const state = new ComputedState<number>(() => ++counter, { updateDelayer: FixedDelayer.zero });

    await state.whenFirstTimeUpdated();
    expect(state.value).toBe(1);

    state.computed?.invalidate();
    await delay(20);
    expect(state.updateIndex).toBe(2);
    expect(state.value).toBe(2);
  });

  it("should auto-update on invalidation with FixedDelayer", async () => {
    let counter = 0;
    const state = new ComputedState<number>(() => ++counter, { updateDelayer: new FixedDelayer(30).delay });

    await state.whenFirstTimeUpdated();
    expect(state.value).toBe(1);

    state.computed?.invalidate();

    // Should not have updated yet
    await delay(10);
    expect(state.value).toBe(1);

    // Wait for delay + update
    await delay(50);
    expect(state.value).toBe(2);
  });

  it("should recompute after invalidation and resolve whenUpdated", async () => {
    let counter = 0;
    const state = new ComputedState<number>(() => ++counter, { updateDelayer: FixedDelayer.zero });

    await state.whenFirstTimeUpdated();
    expect(state.updateIndex).toBe(1);

    const updated = state.whenUpdated();
    state.computed.invalidate();
    await updated;

    expect(state.updateIndex).toBe(2);
    expect(state.value).toBe(2);
  });

  it("should track lastNonErrorValue", async () => {
    let counter = 0;
    const state = new ComputedState<number>(() => {
      counter++;
      if (counter === 2) throw new Error("transient");
      return counter;
    }, { updateDelayer: FixedDelayer.zero });

    await state.whenFirstTimeUpdated();
    expect(state.value).toBe(1);
    expect(state.lastNonErrorValue).toBe(1);

    state.computed?.invalidate();
    await delay(20);

    expect(state.error).toBeDefined();
    expect(state.lastNonErrorValue).toBe(1);
  });

  it("should not update after dispose", async () => {
    let counter = 0;
    const state = new ComputedState<number>(() => ++counter, { updateDelayer: FixedDelayer.zero });

    await state.whenFirstTimeUpdated();
    const computed = state.computed;

    state.dispose();
    expect(state.isDisposed).toBe(true);

    computed?.invalidate();
    await delay(20);

    expect(counter).toBe(1);
  });

  it("should accept initialValue option for async computers", async () => {
    let resolveComputer!: (v: number) => void;
    const state = new ComputedState<number>(
      () => new Promise<number>((r) => { resolveComputer = r; }),
      { initialValue: 99 },
    );

    expect(state.updateIndex).toBe(0);
    expect(state.value).toBe(99);

    resolveComputer(42);
    await state.whenFirstTimeUpdated();
    expect(state.updateIndex).toBe(1);
    expect(state.value).toBe(42);
  });
});
