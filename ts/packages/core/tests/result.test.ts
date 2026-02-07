import { describe, it, expect } from "vitest";
import { ok, error, resultFrom, resultFromAsync, resultValue } from "../src/index.js";

describe("Result", () => {
  it("ok should create a successful result", () => {
    const r = ok(42);
    expect(r.ok).toBe(true);
    expect(r.value).toBe(42);
  });

  it("error should create a failed result", () => {
    const r = error(new Error("fail"));
    expect(r.ok).toBe(false);
    expect(r.error).toBeInstanceOf(Error);
  });

  it("resultFrom should capture success", () => {
    const r = resultFrom(() => 42);
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.value).toBe(42);
  });

  it("resultFrom should capture thrown error", () => {
    const r = resultFrom(() => { throw new Error("boom"); });
    expect(r.ok).toBe(false);
    if (!r.ok) expect((r.error as Error).message).toBe("boom");
  });

  it("resultFromAsync should capture async success", async () => {
    const r = await resultFromAsync(async () => 42);
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.value).toBe(42);
  });

  it("resultFromAsync should capture async error", async () => {
    const r = await resultFromAsync(async () => { throw new Error("async boom"); });
    expect(r.ok).toBe(false);
  });

  it("resultValue should unwrap ok", () => {
    expect(resultValue(ok(42))).toBe(42);
  });

  it("resultValue should throw on error", () => {
    expect(() => resultValue(error(new Error("fail")))).toThrow("fail");
  });
});
