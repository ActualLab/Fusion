import { describe, it, expect } from "vitest";
import { getInstanceId, StateBase, MutableState } from "../src/index.js";

describe("getInstanceId", () => {
  it("should return same id for same object", () => {
    const obj = {};
    expect(getInstanceId(obj)).toBe(getInstanceId(obj));
  });

  it("should return different ids for different objects", () => {
    expect(getInstanceId({})).not.toBe(getInstanceId({}));
  });
});

describe("StateBase", () => {
  it("should be an instance of StateBase", () => {
    const s = new MutableState(0);
    expect(s).toBeInstanceOf(StateBase);
  });

  it("should distinguish state inputs from string inputs by type", () => {
    const strInput = "some-key";
    const stateInput = new MutableState(42);
    expect(typeof strInput).toBe("string");
    expect(stateInput).toBeInstanceOf(StateBase);
  });
});
