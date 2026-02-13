import { describe, it, expect, beforeEach } from "vitest";
import { AsyncContext, errorResult } from "@actuallab/core";
import { Computed, ConsistencyState, ComputeContext, computeContextKey, computeMethod, wrapComputeMethod, MutableState } from "../src/index.js";

let _testKeyCounter = 0;
function makeKey(method: string, ...args: unknown[]): string {
  return `Test.${method}[${++_testKeyCounter}]:${args.map(a => JSON.stringify(a)).join(",")}`;
}

describe("Computed", () => {
  beforeEach(() => {
    AsyncContext.current = undefined;
  });

  it("should start in Computing state", () => {
    const c = new Computed<number>(makeKey("get", 1));
    expect(c.state).toBe(ConsistencyState.Computing);
    expect(() => c.output).toThrow("no output");
  });

  it("should transition to Consistent after setOutput with value", () => {
    const c = new Computed<number>(makeKey("get", 1));
    c.setOutput(42);
    expect(c.state).toBe(ConsistencyState.Consistent);
    expect(c.isConsistent).toBe(true);
    expect(c.value).toBe(42);
    expect(c.hasValue).toBe(true);
  });

  it("should transition to Consistent after setOutput with error", () => {
    const c = new Computed<number>(makeKey("get", 1));
    c.setOutput(errorResult(new Error("boom")));
    expect(c.state).toBe(ConsistencyState.Consistent);
    expect(c.hasError).toBe(true);
    expect(() => c.value).toThrow("boom");
  });

  it("should throw when accessing value before output set", () => {
    const c = new Computed<number>(makeKey("get", 1));
    expect(() => c.value).toThrow("no output");
  });

  it("should invalidate from Consistent state", () => {
    const c = new Computed<number>(makeKey("get", 1));
    c.setOutput(42);
    c.invalidate();
    expect(c.state).toBe(ConsistencyState.Invalidated);
  });

  it("should be idempotent on invalidation", () => {
    const c = new Computed<number>(makeKey("get", 1));
    c.setOutput(42);
    let count = 0;
    c.onInvalidated.add(() => count++);
    c.invalidate();
    c.invalidate();
    expect(count).toBe(1);
  });

  it("should fire onInvalidated callback", () => {
    const c = new Computed<number>(makeKey("get", 1));
    c.setOutput(42);
    let fired = false;
    c.onInvalidated.add(() => { fired = true; });
    c.invalidate();
    expect(fired).toBe(true);
  });
});

describe("Computed dependency tracking", () => {
  beforeEach(() => {
    AsyncContext.current = undefined;
  });

  it("should track forward dependencies", () => {
    const parent = new Computed<number>(makeKey("parent", 1));
    const child = new Computed<number>(makeKey("child", 1));
    child.setOutput(10);

    parent.addDependency(child);
    expect(parent.dependencies.has(child)).toBe(true);
  });

  it("should cascade invalidation from child to parent", () => {
    const parent = new Computed<number>(makeKey("parent", 1));
    parent.setOutput(100);

    const child = new Computed<number>(makeKey("child", 1));
    child.setOutput(10);

    parent.addDependency(child);

    child.invalidate();
    expect(child.state).toBe(ConsistencyState.Invalidated);
    expect(parent.state).toBe(ConsistencyState.Invalidated);
  });

  it("should cascade invalidation through multiple levels", () => {
    const a = new Computed<number>(makeKey("a", 1));
    a.setOutput(1);

    const b = new Computed<number>(makeKey("b", 1));
    b.setOutput(2);

    const c = new Computed<number>(makeKey("c", 1));
    c.setOutput(3);

    // a depends on b, b depends on c
    a.addDependency(b);
    b.addDependency(c);

    c.invalidate();
    expect(c.state).toBe(ConsistencyState.Invalidated);
    expect(b.state).toBe(ConsistencyState.Invalidated);
    expect(a.state).toBe(ConsistencyState.Invalidated);
  });

  it("should not cascade to replaced dependant (different version)", () => {
    const parentKey = makeKey("parent", 1);
    const parent1 = new Computed<number>(parentKey);
    parent1.setOutput(100);

    const child = new Computed<number>(makeKey("child", 1));
    child.setOutput(10);

    parent1.addDependency(child);

    // Replace parent with a new version (different Computed, same key)
    const parent2 = new Computed<number>(parentKey);
    parent2.setOutput(200);

    // Invalidate child — parent1 gets invalidated (it added the dependency),
    // but parent2 is unrelated and stays consistent
    child.invalidate();
    expect(parent1.state).toBe(ConsistencyState.Invalidated);
    expect(parent2.state).toBe(ConsistencyState.Consistent);
  });
});

describe("Computed.use() dependency capture", () => {
  beforeEach(() => {
    AsyncContext.current = undefined;
  });

  it("should capture dependency when used inside ComputeContext via AsyncContext", () => {
    const parent = new Computed<number>(makeKey("parent", 1));
    const child = new Computed<number>(makeKey("child", 1));
    child.setOutput(42);

    const ctx = new ComputeContext(parent as Computed<unknown>);
    AsyncContext.current = new AsyncContext().with(computeContextKey, ctx);

    const val = child.use();
    expect(val).toBe(42);
    expect(parent.dependencies.has(child)).toBe(true);

    AsyncContext.current = undefined;
  });

  it("should not capture when no AsyncContext is active", () => {
    const child = new Computed<number>(makeKey("child", 1));
    child.setOutput(42);

    // No active context — just returns value, no side effects
    const val = child.use();
    expect(val).toBe(42);
  });

  it("should return stale value with useInconsistent() on invalidated computed", () => {
    const c = new Computed<number>(makeKey("get", 1));
    c.setOutput(42);
    c.invalidate();

    const val = c.useInconsistent();
    expect(val).toBe(42);
  });

  it("should throw with useInconsistent() on computed with no output", () => {
    const c = new Computed<number>(makeKey("get", 1));
    expect(() => c.useInconsistent()).toThrow("no output");
  });

  it("should recompute via renewer when invalidated", async () => {
    const renewed = new Computed<number>(makeKey("get", 2));
    renewed.setOutput(99);

    const c = new Computed<number>(makeKey("get", 1), () => renewed);
    c.setOutput(42);
    c.invalidate();

    const val = c.use();
    expect(val).toBe(99);
  });

  it("should recompute via renewer returning Promise", async () => {
    const renewed = new Computed<number>(makeKey("get", 2));
    renewed.setOutput(77);

    const c = new Computed<number>(makeKey("get", 1), () => Promise.resolve(renewed));
    c.setOutput(42);
    c.invalidate();

    const val = await c.use();
    expect(val).toBe(77);
  });

  it("should throw when invalidated with no renewer", () => {
    const c = new Computed<number>(makeKey("get", 1));
    c.setOutput(42);
    c.invalidate();

    expect(() => c.use()).toThrow("Cannot recompute");
  });
});

describe("Computed.capture()", () => {
  beforeEach(() => {
    AsyncContext.current = undefined;
  });

  it("should capture a @computeMethod result", async () => {
    class Svc {
      @computeMethod
      async getValue(key: string): Promise<number> {
        return key === "x" ? 42 : 0;
      }
    }
    const svc = new Svc();

    const captured = await Computed.capture(() => svc.getValue("x"));
    expect(captured.value).toBe(42);
    expect(captured.isConsistent).toBe(true);
  });

  it("should observe invalidation on captured computed", async () => {
    const source = new MutableState(10);

    class Svc {
      @computeMethod
      async getValue(key: string): Promise<number> {
        return source.use();
      }
    }
    const svc = new Svc();

    const captured = await Computed.capture(() => svc.getValue("a"));
    expect(captured.value).toBe(10);
    expect(captured.isConsistent).toBe(true);

    source.set(20);
    await captured.whenInvalidated();
    expect(captured.isConsistent).toBe(false);
  });

  it("should capture a wrapComputeMethod function", async () => {
    const getDouble = wrapComputeMethod(async (n: number) => n * 2);

    const captured = await Computed.capture(() => getDouble(5));
    expect(captured.value).toBe(10);
    expect(captured.isConsistent).toBe(true);
  });

  it("should throw when fn does not call a compute function", async () => {
    await expect(Computed.capture(() => 42)).rejects.toThrow("No Computed was captured");
  });
});
