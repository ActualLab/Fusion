import { describe, it, expect, beforeEach } from "vitest";
import { Computed, ComputedInput, computedRegistry } from "../src/index.js";

function makeInput(method: string, ...args: unknown[]): ComputedInput {
  return new ComputedInput("Test", method, args);
}

describe("ComputedRegistry", () => {
  beforeEach(() => {
    computedRegistry.clear();
  });

  it("should register and retrieve computed", () => {
    const input = makeInput("get", 1);
    const computed = new Computed<number>(input);
    computed.setOutput(42);
    computedRegistry.register(computed);

    const retrieved = computedRegistry.get(input);
    expect(retrieved).toBe(computed);
  });

  it("should return undefined for missing input", () => {
    const input = makeInput("get", 999);
    expect(computedRegistry.get(input)).toBeUndefined();
  });

  it("should overwrite on re-register", () => {
    const input = makeInput("get", 1);
    const c1 = new Computed<number>(input);
    c1.setOutput(1);
    computedRegistry.register(c1);

    const c2 = new Computed<number>(input);
    c2.setOutput(2);
    computedRegistry.register(c2);

    const retrieved = computedRegistry.get(input);
    expect(retrieved).toBe(c2);
  });

  it("should remove entries", () => {
    const input = makeInput("get", 1);
    const computed = new Computed<number>(input);
    computedRegistry.register(computed);
    computedRegistry.remove(input);
    expect(computedRegistry.get(input)).toBeUndefined();
  });

  it("should track size", () => {
    expect(computedRegistry.size).toBe(0);
    const input = makeInput("get", 1);
    computedRegistry.register(new Computed<number>(input));
    expect(computedRegistry.size).toBe(1);
    computedRegistry.clear();
    expect(computedRegistry.size).toBe(0);
  });
});
