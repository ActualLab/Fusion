import { describe, it, expect } from "vitest";
import { ComputedInput, ComputeMethodInput, getInstanceId } from "../src/index.js";

describe("ComputedInput", () => {
  it("should store and return key", () => {
    const input = new ComputedInput("my-key");
    expect(input.key).toBe("my-key");
  });

  it("should compare by key", () => {
    const a = new ComputedInput("same");
    const b = new ComputedInput("same");
    expect(a.equals(b)).toBe(true);
  });

  it("should not equal different key", () => {
    const a = new ComputedInput("x");
    const b = new ComputedInput("y");
    expect(a.equals(b)).toBe(false);
  });

  it("toString should return the key", () => {
    const input = new ComputedInput("my-key");
    expect(input.toString()).toBe("my-key");
  });
});

describe("ComputeMethodInput", () => {
  it("should produce consistent keys for same instance+method+args", () => {
    const instance = {};
    const a = new ComputeMethodInput(instance, "getX", [1, "hello"]);
    const b = new ComputeMethodInput(instance, "getX", [1, "hello"]);
    expect(a.key).toBe(b.key);
    expect(a.equals(b)).toBe(true);
  });

  it("should produce different keys for different args", () => {
    const instance = {};
    const a = new ComputeMethodInput(instance, "getX", [1]);
    const b = new ComputeMethodInput(instance, "getX", [2]);
    expect(a.key).not.toBe(b.key);
  });

  it("should produce different keys for different methods", () => {
    const instance = {};
    const a = new ComputeMethodInput(instance, "getX", [1]);
    const b = new ComputeMethodInput(instance, "getY", [1]);
    expect(a.key).not.toBe(b.key);
  });

  it("should produce different keys for different instances", () => {
    const a = new ComputeMethodInput({}, "getX", [1]);
    const b = new ComputeMethodInput({}, "getX", [1]);
    expect(a.key).not.toBe(b.key);
  });

  it("should store instance, methodName, and args", () => {
    const instance = { name: "test" };
    const input = new ComputeMethodInput(instance, "getValue", ["a", 42]);
    expect(input.instance).toBe(instance);
    expect(input.methodName).toBe("getValue");
    expect(input.args).toEqual(["a", 42]);
  });
});

describe("getInstanceId", () => {
  it("should return same id for same object", () => {
    const obj = {};
    expect(getInstanceId(obj)).toBe(getInstanceId(obj));
  });

  it("should return different ids for different objects", () => {
    expect(getInstanceId({})).not.toBe(getInstanceId({}));
  });
});
