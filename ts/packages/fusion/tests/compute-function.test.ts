import { describe, it, expect, beforeEach } from "vitest";
import {
  ComputeFunction,
  ComputeContext,
  ConsistencyState,
  computedRegistry,
} from "../src/index.js";

describe("ComputeFunction", () => {
  beforeEach(() => {
    computedRegistry.clear();
    ComputeContext.current = undefined;
  });

  it("should produce a Consistent computed with the function result", async () => {
    const fn = new ComputeFunction("Svc", "double", (x: unknown) => (x as number) * 2);
    const computed = await fn.invoke([5]);

    expect(computed.state).toBe(ConsistencyState.Consistent);
    expect(computed.value).toBe(10);
  });

  it("should cache the result for the same args", async () => {
    let callCount = 0;
    const fn = new ComputeFunction("Svc", "count", () => ++callCount);

    const c1 = await fn.invoke([1]);
    const c2 = await fn.invoke([1]);

    expect(c1).toBe(c2);
    expect(callCount).toBe(1);
  });

  it("should produce different computed for different args", async () => {
    const fn = new ComputeFunction("Svc", "id", (x: unknown) => x);

    const c1 = await fn.invoke([1]);
    const c2 = await fn.invoke([2]);

    expect(c1).not.toBe(c2);
    expect(c1.value).toBe(1);
    expect(c2.value).toBe(2);
  });

  it("should recompute after invalidation", async () => {
    let callCount = 0;
    const fn = new ComputeFunction("Svc", "inc", () => ++callCount);

    const c1 = await fn.invoke([1]);
    expect(c1.value).toBe(1);

    c1.invalidate();

    const c2 = await fn.invoke([1]);
    expect(c2.value).toBe(2);
    expect(c2).not.toBe(c1);
  });

  it("should capture errors in computed", async () => {
    const fn = new ComputeFunction("Svc", "fail", () => {
      throw new Error("compute error");
    });

    const computed = await fn.invoke([1]);
    expect(computed.state).toBe(ConsistencyState.Consistent);
    expect(computed.output?.ok).toBe(false);
    expect(() => computed.value).toThrow("compute error");
  });

  it("should handle async compute functions", async () => {
    const fn = new ComputeFunction("Svc", "async", async (x: unknown) => {
      await new Promise((r) => setTimeout(r, 5));
      return (x as number) + 1;
    });

    const computed = await fn.invoke([10]);
    expect(computed.value).toBe(11);
  });

  it("should capture dependencies between compute functions", async () => {
    const innerFn = new ComputeFunction("Svc", "inner", (x: unknown) => (x as number) * 2);
    const outerFn = new ComputeFunction("Svc", "outer", async (x: unknown) => {
      const innerComputed = await innerFn.invoke([x]);
      return innerComputed.use() + 1;
    });

    const outer = await outerFn.invoke([5]);
    expect(outer.value).toBe(11); // inner(5)=10, outer=10+1=11

    // outer should depend on inner
    expect(outer.dependencies.size).toBe(1);
  });
});
