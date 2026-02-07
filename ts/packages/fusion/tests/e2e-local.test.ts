import { describe, it, expect, beforeEach } from "vitest";
import {
  defineComputeService,
  createLocalService,
  ComputeFunction,
  ComputedState,
  MutableState,
  ComputeContext,
  ComputedInput,
  computedRegistry,
  NoDelayer,
} from "../src/index.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

describe("End-to-end local Fusion", () => {
  beforeEach(() => {
    computedRegistry.clear();
    ComputeContext.current = undefined;
  });

  it("should cascade invalidation through MutableState → ComputeFunction → ComputedState", async () => {
    const source = new MutableState(10);

    // A compute function that reads from the mutable state
    const doubled = new ComputeFunction("Calc", "doubled", () => {
      return source.value * 2;
    });

    // Wrap in ComputedState for auto-update
    const state = new ComputedState<number>(() => doubled.invoke([]), new NoDelayer());

    await state.initialize();
    expect(state.value).toBe(20);

    // Mutate the source — invalidates MutableState's computed,
    // which should cascade to `doubled`'s computed
    source.set(50);

    await delay(50);
    expect(state.value).toBe(100);
  });

  it("should cascade invalidation through nested compute functions", async () => {
    const source = new MutableState(5);

    const baseFn = new ComputeFunction("Svc", "base", () => source.value);
    const derivedFn = new ComputeFunction("Svc", "derived", async () => {
      const baseComputed = await baseFn.invoke([]);
      return baseComputed.use() + 100;
    });

    const state = new ComputedState<number>(() => derivedFn.invoke([]), new NoDelayer());

    await state.initialize();
    expect(state.value).toBe(105); // 5 + 100

    source.set(20);
    await delay(50);
    expect(state.value).toBe(120); // 20 + 100
  });

  it("should cascade through multiple MutableState dependencies", async () => {
    const price = new MutableState(100);
    const quantity = new MutableState(3);

    const totalFn = new ComputeFunction("Order", "total", () => {
      return price.value * quantity.value;
    });

    const state = new ComputedState<number>(() => totalFn.invoke([]), new NoDelayer());

    await state.initialize();
    expect(state.value).toBe(300);

    price.set(200);
    await delay(50);
    expect(state.value).toBe(600); // 200 * 3

    quantity.set(5);
    await delay(50);
    expect(state.value).toBe(1000); // 200 * 5
  });

  it("should work with local compute service proxy", async () => {
    const store: Record<string, number> = { x: 10 };

    const svcDef = defineComputeService("DataService", {
      getValue: { args: [""] },
    });

    const svc = createLocalService(svcDef, {
      getValue(key: unknown) {
        return store[key as string] ?? 0;
      },
    });

    // First call computes and caches
    const r1 = await svc.getValue("x");
    expect(r1).toBe(10);

    // Second call returns cached
    const r2 = await svc.getValue("x");
    expect(r2).toBe(10);

    // Invalidate via registry
    const input = new ComputedInput("DataService", "getValue", ["x"]);
    const cached = computedRegistry.get(input);
    expect(cached).toBeDefined();
    cached?.invalidate();

    // Mutate
    store["x"] = 99;

    // Re-compute
    const r3 = await svc.getValue("x");
    expect(r3).toBe(99);
  });
});
