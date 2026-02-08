import { describe, it, expect } from "vitest";
import { getInstanceId, StateBase, inputKey, MutableState } from "../src/index.js";

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
  it("should assign unique stateKey per instance", () => {
    const a = new MutableState(1);
    const b = new MutableState(2);
    expect(a.stateKey).not.toBe(b.stateKey);
  });

  it("should include prefix in stateKey", () => {
    const s = new MutableState(42);
    expect(s.stateKey).toMatch(/^MutableState#\d+$/);
  });

  it("should be an instance of StateBase", () => {
    const s = new MutableState(0);
    expect(s).toBeInstanceOf(StateBase);
  });
});

describe("inputKey", () => {
  it("should return string input as-is", () => {
    expect(inputKey("my-key")).toBe("my-key");
  });

  it("should return stateKey for StateBase input", () => {
    const s = new MutableState(0);
    expect(inputKey(s)).toBe(s.stateKey);
  });
});
