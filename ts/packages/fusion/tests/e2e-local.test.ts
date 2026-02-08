import { describe, it, expect, beforeEach } from "vitest";
import { AsyncContext } from "@actuallab/core";
import {
  computeMethod,
  ComputeFunction,
  ComputedState,
  MutableState,
  computedRegistry,
  NoDelayer,
} from "../src/index.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

describe("End-to-end local Fusion", () => {
  beforeEach(() => {
    computedRegistry.clear();
    AsyncContext.current = undefined;
  });

  it("should cascade invalidation through MutableState → ComputeFunction → ComputedState", async () => {
    const source = new MutableState(10);

    // A compute function that reads from the mutable state
    const doubled = new ComputeFunction("doubled", function() {
      return source.value * 2;
    });
    const instance = {};

    // Wrap in ComputedState for auto-update
    const state = new ComputedState<number>(() => doubled.invoke(instance, []), { delayer: new NoDelayer() });

    await state.initialize();
    expect(state.value).toBe(20);

    // Mutate the source
    source.set(50);

    await delay(50);
    expect(state.value).toBe(100);
  });

  it("should cascade invalidation through nested compute functions", async () => {
    const source = new MutableState(5);
    const instance = {};

    const baseFn = new ComputeFunction("base", function() { return source.value; });
    const derivedFn = new ComputeFunction("derived", async function() {
      const baseComputed = await baseFn.invoke(instance, []);
      return baseComputed.use() + 100;
    });

    const state = new ComputedState<number>(() => derivedFn.invoke(instance, []), { delayer: new NoDelayer() });

    await state.initialize();
    expect(state.value).toBe(105); // 5 + 100

    source.set(20);
    await delay(50);
    expect(state.value).toBe(120); // 20 + 100
  });

  it("should cascade through multiple MutableState dependencies", async () => {
    const price = new MutableState(100);
    const quantity = new MutableState(3);
    const instance = {};

    const totalFn = new ComputeFunction("total", function() {
      return price.value * quantity.value;
    });

    const state = new ComputedState<number>(() => totalFn.invoke(instance, []), { delayer: new NoDelayer() });

    await state.initialize();
    expect(state.value).toBe(300);

    price.set(200);
    await delay(50);
    expect(state.value).toBe(600); // 200 * 3

    quantity.set(5);
    await delay(50);
    expect(state.value).toBe(1000); // 200 * 5
  });

  it("should work with @computeMethod decorator", async () => {
    class CounterService {
      private store = new Map<string, number>();

      @computeMethod
      async getValue(id: string): Promise<number> {
        return this.store.get(id) ?? 0;
      }

      async increment(id: string): Promise<void> {
        this.store.set(id, (this.store.get(id) ?? 0) + 1);
        this.getValue.invalidate(id);
      }
    }

    const svc = new CounterService();

    // Initial value
    expect(await svc.getValue("x")).toBe(0);

    // Cached
    expect(await svc.getValue("x")).toBe(0);

    // Mutate and invalidate
    await svc.increment("x");
    expect(await svc.getValue("x")).toBe(1);

    await svc.increment("x");
    expect(await svc.getValue("x")).toBe(2);
  });
});
