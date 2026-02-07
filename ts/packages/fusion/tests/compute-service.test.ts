import { describe, it, expect, beforeEach } from "vitest";
import {
  defineComputeService,
  createLocalService,
  ComputeContext,
  computedRegistry,
} from "../src/index.js";

const counterServiceDef = defineComputeService("CounterService", {
  getValue: { args: [""] },
  getDoubled: { args: [""] },
});

describe("createLocalService", () => {
  let store: Record<string, number>;

  beforeEach(() => {
    computedRegistry.clear();
    ComputeContext.current = undefined;
    store = { a: 1, b: 2 };
  });

  it("should intercept compute methods and cache results", async () => {
    let callCount = 0;
    const svc = createLocalService(counterServiceDef, {
      getValue(key: unknown) {
        callCount++;
        return store[key as string] ?? 0;
      },
      getDoubled(key: unknown) {
        return (store[key as string] ?? 0) * 2;
      },
    });

    const r1 = await svc.getValue("a");
    const r2 = await svc.getValue("a");

    expect(r1).toBe(1);
    expect(r2).toBe(1);
    expect(callCount).toBe(1); // cached
  });

  it("should produce different results for different args", async () => {
    const svc = createLocalService(counterServiceDef, {
      getValue(key: unknown) { return store[key as string] ?? 0; },
      getDoubled(key: unknown) { return (store[key as string] ?? 0) * 2; },
    });

    const a = await svc.getValue("a");
    const b = await svc.getValue("b");
    expect(a).toBe(1);
    expect(b).toBe(2);
  });

  it("should recompute after invalidation of registry entry", async () => {
    let callCount = 0;
    const svc = createLocalService(counterServiceDef, {
      getValue(key: unknown) {
        callCount++;
        return store[key as string] ?? 0;
      },
      getDoubled() { return 0; },
    });

    await svc.getValue("a"); // callCount=1, cached

    // Invalidate by finding the computed in the registry
    const input = computedRegistry.get(
      new (await import("../src/index.js")).ComputedInput("CounterService", "getValue", ["a"])
    );
    expect(input).toBeDefined();
    input?.invalidate();

    // Mutate the store
    store["a"] = 100;

    const result = await svc.getValue("a");
    expect(result).toBe(100);
    expect(callCount).toBe(2);
  });

  it("should track dependencies between compute methods", async () => {
    const innerDef = defineComputeService("Inner", {
      getBase: { args: [""] },
    });
    const outerDef = defineComputeService("Outer", {
      getDerived: { args: [""] },
    });

    const innerSvc = createLocalService(innerDef, {
      getBase(key: unknown) { return store[key as string] ?? 0; },
    });

    const outerSvc = createLocalService(outerDef, {
      async getDerived(key: unknown) {
        const base = await innerSvc.getBase(key);
        return (base as number) * 10;
      },
    });

    const result = await outerSvc.getDerived("a");
    expect(result).toBe(10);
  });
});
