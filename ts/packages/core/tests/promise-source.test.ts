import { describe, it, expect } from "vitest";
import { PromiseSource } from "../src/index.js";

describe("PromiseSource", () => {
  it("should resolve with value", async () => {
    const ps = new PromiseSource<number>();
    expect(ps.isCompleted).toBe(false);
    ps.resolve(42);
    expect(ps.isCompleted).toBe(true);
    expect(await ps.promise).toBe(42);
  });

  it("should reject with error", async () => {
    const ps = new PromiseSource<number>();
    ps.reject(new Error("fail"));
    expect(ps.isCompleted).toBe(true);
    await expect(ps.promise).rejects.toThrow("fail");
  });

  it("should return false on duplicate resolve", () => {
    const ps = new PromiseSource<number>();
    expect(ps.resolve(1)).toBe(true);
    expect(ps.resolve(2)).toBe(false);
  });

  it("should return false on resolve after reject", async () => {
    const ps = new PromiseSource<number>();
    expect(ps.reject(new Error("fail"))).toBe(true);
    expect(ps.resolve(42)).toBe(false);
    // Consume the rejection to avoid unhandled rejection warning
    await expect(ps.promise).rejects.toThrow("fail");
  });
});
