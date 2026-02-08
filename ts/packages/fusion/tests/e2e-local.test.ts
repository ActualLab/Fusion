import { describe, it, expect, beforeEach } from "vitest";
import { AsyncContext } from "@actuallab/core";
import {
  computeMethod,
  ComputeFunction,
  ComputedState,
  MutableState,
  FixedDelayer,
} from "../src/index.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

describe("End-to-end local Fusion", () => {
  beforeEach(() => {
    AsyncContext.current = undefined;
  });

  it("should cascade invalidation through MutableState → ComputeFunction → ComputedState", async () => {
    const source = new MutableState(10);

    const state = new ComputedState<number>(() => source.use() * 2, { updateDelayer: FixedDelayer.zero });

    await state.whenFirstTimeUpdated();
    expect(state.value).toBe(20);

    source.set(50);
    await delay(50);
    expect(state.value).toBe(100);
  });

  it("should cascade invalidation through nested compute functions", async () => {
    const source = new MutableState(5);
    const instance = {};

    const baseFn = new ComputeFunction("base", function() { return source.use(); });
    const derivedFn = new ComputeFunction("derived", async function() {
      const baseComputed = await baseFn.invoke(instance, []);
      return (baseComputed.value as number) + 100;
    });

    const state = new ComputedState<number>(
      async () => {
        const computed = await derivedFn.invoke(instance, []);
        return computed.value as number;
      },
      { updateDelayer: FixedDelayer.zero },
    );

    await state.whenFirstTimeUpdated();
    expect(state.value).toBe(105);

    source.set(20);
    await delay(50);
    expect(state.value).toBe(120);
  });

  it("should cascade through multiple MutableState dependencies", async () => {
    const price = new MutableState(100);
    const quantity = new MutableState(3);

    const state = new ComputedState<number>(() => price.use() * quantity.use(), { updateDelayer: FixedDelayer.zero });

    await state.whenFirstTimeUpdated();
    expect(state.value).toBe(300);

    price.set(200);
    await delay(50);
    expect(state.value).toBe(600);

    quantity.set(5);
    await delay(50);
    expect(state.value).toBe(1000);
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
        (this.getValue as any).invalidate(id);
      }
    }

    const svc = new CounterService();

    expect(await svc.getValue("x")).toBe(0);
    expect(await svc.getValue("x")).toBe(0);

    await svc.increment("x");
    expect(await svc.getValue("x")).toBe(1);

    await svc.increment("x");
    expect(await svc.getValue("x")).toBe(2);
  });
});
