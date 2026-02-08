import { describe, it, expect, beforeEach } from "vitest";
import { AsyncContext } from "@actuallab/core";
import {
  ComputeFunction,
  ConsistencyState,
  computedRegistry,
} from "../src/index.js";

const testInstance = { name: "testService" };

describe("ComputeFunction", () => {
  beforeEach(() => {
    computedRegistry.clear();
    AsyncContext.current = undefined;
  });

  it("should produce a Consistent computed with the function result", async () => {
    const fn = new ComputeFunction("double", function(this: any, x: unknown) { return (x as number) * 2; });
    const computed = await fn.invoke(testInstance, [5]);

    expect(computed.state).toBe(ConsistencyState.Consistent);
    expect(computed.value).toBe(10);
  });

  it("should cache the result for the same instance+args", async () => {
    let callCount = 0;
    const fn = new ComputeFunction("count", function() { return ++callCount; });

    const c1 = await fn.invoke(testInstance, [1]);
    const c2 = await fn.invoke(testInstance, [1]);

    expect(c1).toBe(c2);
    expect(callCount).toBe(1);
  });

  it("should produce different computed for different args", async () => {
    const fn = new ComputeFunction("id", function(this: any, x: unknown) { return x; });

    const c1 = await fn.invoke(testInstance, [1]);
    const c2 = await fn.invoke(testInstance, [2]);

    expect(c1).not.toBe(c2);
    expect(c1.value).toBe(1);
    expect(c2.value).toBe(2);
  });

  it("should recompute after invalidation", async () => {
    let callCount = 0;
    const fn = new ComputeFunction("inc", function() { return ++callCount; });

    const c1 = await fn.invoke(testInstance, [1]);
    expect(c1.value).toBe(1);

    c1.invalidate();

    const c2 = await fn.invoke(testInstance, [1]);
    expect(c2.value).toBe(2);
    expect(c2).not.toBe(c1);
  });

  it("should capture errors in computed", async () => {
    const fn = new ComputeFunction("fail", function() {
      throw new Error("compute error");
    });

    const computed = await fn.invoke(testInstance, [1]);
    expect(computed.state).toBe(ConsistencyState.Consistent);
    expect(computed.output?.ok).toBe(false);
    expect(() => computed.value).toThrow("compute error");
  });

  it("should handle async compute functions", async () => {
    const fn = new ComputeFunction("async", async function(this: any, x: unknown) {
      await new Promise((r) => setTimeout(r, 5));
      return (x as number) + 1;
    });

    const computed = await fn.invoke(testInstance, [10]);
    expect(computed.value).toBe(11);
  });

  it("should capture dependencies between compute functions", async () => {
    const innerFn = new ComputeFunction("inner", function(this: any, x: unknown) { return (x as number) * 2; });
    const outerFn = new ComputeFunction("outer", async function(this: any, x: unknown) {
      const innerComputed = await innerFn.invoke(testInstance, [x]);
      return innerComputed.use() + 1;
    });

    const outer = await outerFn.invoke(testInstance, [5]);
    expect(outer.value).toBe(11); // inner(5)=10, outer=10+1=11

    // outer should depend on inner
    expect(outer.dependencies.size).toBe(1);
  });
});
