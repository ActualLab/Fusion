import { describe, it, expect } from "vitest";
import { result, errorResult, resultFrom, resultFromAsync } from "../src/index.js";

describe("Result", () => {
  it("result should create a successful result", () => {
    const r = result(42);
    expect(r.hasValue).toBe(true);
    expect(r.value).toBe(42);
  });

  it("errorResult should create a failed result", () => {
    const r = errorResult(new Error("fail"));
    expect(r.hasValue).toBe(false);
    expect(r.error).toBeInstanceOf(Error);
  });

  it("resultFrom should capture success", () => {
    const r = resultFrom(() => 42);
    expect(r.hasValue).toBe(true);
    expect(r.value).toBe(42);
  });

  it("resultFrom should capture thrown error", () => {
    const r = resultFrom(() => { throw new Error("boom"); });
    expect(r.hasError).toBe(true);
    expect((r.error as Error).message).toBe("boom");
  });

  it("resultFromAsync should capture async success", async () => {
    const r = await resultFromAsync(async () => 42);
    expect(r.hasValue).toBe(true);
    expect(r.value).toBe(42);
  });

  it("resultFromAsync should capture async error", async () => {
    const r = await resultFromAsync(async () => { throw new Error("async boom"); });
    expect(r.hasError).toBe(true);
  });

  it("value should throw on error result", () => {
    const r = errorResult(new Error("fail"));
    expect(() => r.value).toThrow("fail");
  });

  it("valueOrUndefined should return undefined on error", () => {
    const r = errorResult(new Error("fail"));
    expect(r.valueOrUndefined).toBeUndefined();
  });
});
