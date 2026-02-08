import { describe, it, expect } from "vitest";
import { getInstanceId, MutableState } from "../src/index.js";
import type { ComputedInput } from "../src/index.js";

describe("getInstanceId", () => {
  it("should return same id for same object", () => {
    const obj = {};
    expect(getInstanceId(obj)).toBe(getInstanceId(obj));
  });

  it("should return different ids for different objects", () => {
    expect(getInstanceId({})).not.toBe(getInstanceId({}));
  });
});

describe("ComputedInput", () => {
  it("should accept string keys", () => {
    const input: ComputedInput = "some-key";
    expect(typeof input).toBe("string");
  });

  it("should accept State objects", () => {
    const input: ComputedInput = new MutableState(42);
    expect(typeof input).not.toBe("string");
  });
});
