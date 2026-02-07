import { describe, it, expect } from "vitest";
import { ComputedInput } from "../src/index.js";

describe("ComputedInput", () => {
  it("should produce consistent keys", () => {
    const a = new ComputedInput("Svc", "getX", [1, "hello"]);
    const b = new ComputedInput("Svc", "getX", [1, "hello"]);
    expect(a.key).toBe(b.key);
    expect(a.equals(b)).toBe(true);
  });

  it("should produce different keys for different args", () => {
    const a = new ComputedInput("Svc", "getX", [1]);
    const b = new ComputedInput("Svc", "getX", [2]);
    expect(a.key).not.toBe(b.key);
  });

  it("should produce different keys for different methods", () => {
    const a = new ComputedInput("Svc", "getX", [1]);
    const b = new ComputedInput("Svc", "getY", [1]);
    expect(a.key).not.toBe(b.key);
  });

  it("should produce different keys for different services", () => {
    const a = new ComputedInput("Svc1", "getX", [1]);
    const b = new ComputedInput("Svc2", "getX", [1]);
    expect(a.key).not.toBe(b.key);
  });

  it("toString should return the key", () => {
    const input = new ComputedInput("Svc", "get", [42]);
    expect(input.toString()).toBe(input.key);
  });
});
