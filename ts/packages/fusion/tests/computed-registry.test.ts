import { describe, it, expect, beforeEach } from "vitest";
import { Computed, computedRegistry } from "../src/index.js";

let _testKeyCounter = 0;
function makeKey(method: string, ...args: unknown[]): string {
  return `Test.${method}[${++_testKeyCounter}]:${args.map(a => JSON.stringify(a)).join(",")}`;
}

describe("ComputedRegistry", () => {
  beforeEach(() => {
    computedRegistry.clear();
  });

  it("should register and retrieve computed", () => {
    const key = makeKey("get", 1);
    const computed = new Computed<number>(key);
    computed.setOutput(42);
    computedRegistry.register(computed);

    const retrieved = computedRegistry.get(key);
    expect(retrieved).toBe(computed);
  });

  it("should return undefined for missing input", () => {
    const key = makeKey("get", 999);
    expect(computedRegistry.get(key)).toBeUndefined();
  });

  it("should overwrite on re-register", () => {
    const key = makeKey("get", 1);
    const c1 = new Computed<number>(key);
    c1.setOutput(1);
    computedRegistry.register(c1);

    const c2 = new Computed<number>(key);
    c2.setOutput(2);
    computedRegistry.register(c2);

    const retrieved = computedRegistry.get(key);
    expect(retrieved).toBe(c2);
  });

  it("should remove entries", () => {
    const key = makeKey("get", 1);
    const computed = new Computed<number>(key);
    computedRegistry.register(computed);
    computedRegistry.remove(key);
    expect(computedRegistry.get(key)).toBeUndefined();
  });

  it("should track size", () => {
    expect(computedRegistry.size).toBe(0);
    const key = makeKey("get", 1);
    computedRegistry.register(new Computed<number>(key));
    expect(computedRegistry.size).toBe(1);
    computedRegistry.clear();
    expect(computedRegistry.size).toBe(0);
  });
});
