import { describe, it, expect, beforeEach } from "vitest";
import { Computed, ConsistencyState, ComputedInput, ComputeContext, computedRegistry } from "../src/index.js";

function makeInput(method: string, ...args: unknown[]): ComputedInput {
  return new ComputedInput("Test", method, args);
}

describe("Computed", () => {
  beforeEach(() => {
    computedRegistry.clear();
    ComputeContext.current = undefined;
  });

  it("should start in Computing state", () => {
    const c = new Computed<number>(makeInput("get", 1));
    expect(c.state).toBe(ConsistencyState.Computing);
    expect(c.output).toBeUndefined();
  });

  it("should transition to Consistent after setOutput", () => {
    const c = new Computed<number>(makeInput("get", 1));
    c.setOutput(42);
    expect(c.state).toBe(ConsistencyState.Consistent);
    expect(c.isConsistent).toBe(true);
    expect(c.value).toBe(42);
    expect(c.output?.ok).toBe(true);
  });

  it("should transition to Consistent after setError", () => {
    const c = new Computed<number>(makeInput("get", 1));
    c.setError(new Error("boom"));
    expect(c.state).toBe(ConsistencyState.Consistent);
    expect(c.output?.ok).toBe(false);
    expect(() => c.value).toThrow("boom");
  });

  it("should throw when accessing value before output set", () => {
    const c = new Computed<number>(makeInput("get", 1));
    expect(() => c.value).toThrow("no value");
  });

  it("should invalidate from Consistent state", () => {
    const c = new Computed<number>(makeInput("get", 1));
    c.setOutput(42);
    c.invalidate();
    expect(c.state).toBe(ConsistencyState.Invalidated);
  });

  it("should be idempotent on invalidation", () => {
    const c = new Computed<number>(makeInput("get", 1));
    c.setOutput(42);
    let count = 0;
    c.onInvalidated = () => count++;
    c.invalidate();
    c.invalidate();
    expect(count).toBe(1);
  });

  it("should fire onInvalidated callback", () => {
    const c = new Computed<number>(makeInput("get", 1));
    c.setOutput(42);
    let fired = false;
    c.onInvalidated = () => { fired = true; };
    c.invalidate();
    expect(fired).toBe(true);
  });
});

describe("Computed dependency tracking", () => {
  beforeEach(() => {
    computedRegistry.clear();
    ComputeContext.current = undefined;
  });

  it("should track forward dependencies", () => {
    const parent = new Computed<number>(makeInput("parent", 1));
    const child = new Computed<number>(makeInput("child", 1));
    child.setOutput(10);
    computedRegistry.register(child);

    parent.addDependency(child);
    expect(parent.dependencies.has(child)).toBe(true);
  });

  it("should cascade invalidation from child to parent", () => {
    const parentInput = makeInput("parent", 1);
    const parent = new Computed<number>(parentInput);
    parent.setOutput(100);
    computedRegistry.register(parent);

    const child = new Computed<number>(makeInput("child", 1));
    child.setOutput(10);
    computedRegistry.register(child);

    parent.addDependency(child);

    child.invalidate();
    expect(child.state).toBe(ConsistencyState.Invalidated);
    expect(parent.state).toBe(ConsistencyState.Invalidated);
  });

  it("should cascade invalidation through multiple levels", () => {
    const inputA = makeInput("a", 1);
    const inputB = makeInput("b", 1);
    const inputC = makeInput("c", 1);

    const a = new Computed<number>(inputA);
    a.setOutput(1);
    computedRegistry.register(a);

    const b = new Computed<number>(inputB);
    b.setOutput(2);
    computedRegistry.register(b);

    const c = new Computed<number>(inputC);
    c.setOutput(3);
    computedRegistry.register(c);

    // a depends on b, b depends on c
    a.addDependency(b);
    b.addDependency(c);

    c.invalidate();
    expect(c.state).toBe(ConsistencyState.Invalidated);
    expect(b.state).toBe(ConsistencyState.Invalidated);
    expect(a.state).toBe(ConsistencyState.Invalidated);
  });

  it("should not cascade to wrong version", () => {
    const parentInput = makeInput("parent", 1);
    const parent1 = new Computed<number>(parentInput);
    parent1.setOutput(100);
    computedRegistry.register(parent1);

    const child = new Computed<number>(makeInput("child", 1));
    child.setOutput(10);
    computedRegistry.register(child);

    parent1.addDependency(child);

    // Replace parent with a new version
    const parent2 = new Computed<number>(parentInput);
    parent2.setOutput(200);
    computedRegistry.register(parent2);

    // Invalidate child — should NOT invalidate parent2 (version mismatch)
    child.invalidate();
    expect(parent2.state).toBe(ConsistencyState.Consistent);
  });
});

describe("Computed.use() dependency capture", () => {
  beforeEach(() => {
    computedRegistry.clear();
    ComputeContext.current = undefined;
  });

  it("should capture dependency when used inside ComputeContext", () => {
    const parent = new Computed<number>(makeInput("parent", 1));
    const child = new Computed<number>(makeInput("child", 1));
    child.setOutput(42);

    const ctx = new ComputeContext(parent as Computed<unknown>);
    ComputeContext.current = ctx;

    const val = child.use();
    expect(val).toBe(42);
    expect(parent.dependencies.has(child)).toBe(true);

    ComputeContext.current = undefined;
  });

  it("should not capture when no ComputeContext is active", () => {
    const child = new Computed<number>(makeInput("child", 1));
    child.setOutput(42);

    // No active context — just returns value, no side effects
    const val = child.use();
    expect(val).toBe(42);
  });
});
