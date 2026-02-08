import { describe, it, expect, beforeEach } from "vitest";
import { AsyncContext, AsyncContextKey } from "../src/index.js";

const testKey = new AsyncContextKey<string>("test", "default");
const numKey = new AsyncContextKey<number>("num", 0);

describe("AsyncContext", () => {
  beforeEach(() => {
    AsyncContext.current = undefined;
  });

  it("should return default value for unknown key", () => {
    const ctx = new AsyncContext();
    expect(ctx.get(testKey)).toBe("default");
  });

  it("should store and retrieve typed values", () => {
    const ctx = new AsyncContext().with(testKey, "hello").with(numKey, 42);
    expect(ctx.get(testKey)).toBe("hello");
    expect(ctx.get(numKey)).toBe(42);
  });

  it("with should return a new immutable context", () => {
    const ctx1 = new AsyncContext().with(testKey, "a");
    const ctx2 = ctx1.with(testKey, "b");
    expect(ctx1.get(testKey)).toBe("a");
    expect(ctx2.get(testKey)).toBe("b");
  });

  it("run should set and restore current", () => {
    const ctx = new AsyncContext().with(testKey, "inside");
    expect(AsyncContext.current).toBeUndefined();
    ctx.run(() => {
      expect(AsyncContext.current).toBe(ctx);
      expect(AsyncContext.current?.get(testKey)).toBe("inside");
    });
    expect(AsyncContext.current).toBeUndefined();
  });

  it("run should restore current on exception", () => {
    const ctx = new AsyncContext();
    try {
      ctx.run(() => { throw new Error("boom"); });
    } catch { /* expected */ }
    expect(AsyncContext.current).toBeUndefined();
  });

  it("activate should set current and return disposable to restore", () => {
    const ctx = new AsyncContext().with(testKey, "active");
    const d = ctx.activate();
    expect(AsyncContext.current).toBe(ctx);
    d.dispose();
    expect(AsyncContext.current).toBeUndefined();
  });

  it("setDefault should provide fallback values", () => {
    const customKey = new AsyncContextKey<string>("custom", "key-default");
    AsyncContext.setDefault(customKey, "global-default");
    const ctx = new AsyncContext();
    expect(ctx.get(customKey)).toBe("global-default");
  });

  it("explicit value should override global default", () => {
    const customKey = new AsyncContextKey<string>("custom2", "key-default");
    AsyncContext.setDefault(customKey, "global-default");
    const ctx = new AsyncContext().with(customKey, "explicit");
    expect(ctx.get(customKey)).toBe("explicit");
  });

  it("getOrCreate should return current if set", () => {
    const ctx = new AsyncContext().with(testKey, "exists");
    AsyncContext.current = ctx;
    expect(AsyncContext.getOrCreate()).toBe(ctx);
  });

  it("getOrCreate should return empty singleton if not set", () => {
    const ctx = AsyncContext.getOrCreate();
    expect(ctx).toBe(AsyncContext.empty);
  });

  it("empty should be an immutable singleton", () => {
    expect(AsyncContext.empty).toBeInstanceOf(AsyncContext);
    expect(AsyncContext.empty).toBe(AsyncContext.empty);
    expect(AsyncContext.empty.get(testKey)).toBe("default");
  });

  describe("from", () => {
    it("should return provided context if defined", () => {
      const ctx = new AsyncContext().with(testKey, "provided");
      expect(AsyncContext.from(ctx)).toBe(ctx);
    });

    it("should fall back to current if ctx is undefined", () => {
      const ctx = new AsyncContext().with(testKey, "current");
      AsyncContext.current = ctx;
      expect(AsyncContext.from(undefined)).toBe(ctx);
    });

    it("should return undefined if both ctx and current are undefined", () => {
      expect(AsyncContext.from(undefined)).toBeUndefined();
    });
  });

  describe("fromArgs", () => {
    it("should extract AsyncContext from last arg", () => {
      const ctx = new AsyncContext().with(testKey, "arg");
      expect(AsyncContext.fromArgs(["a", 1, ctx])).toBe(ctx);
    });

    it("should fall back to current if last arg is not AsyncContext", () => {
      const ctx = new AsyncContext().with(testKey, "current");
      AsyncContext.current = ctx;
      expect(AsyncContext.fromArgs(["a", 1])).toBe(ctx);
    });

    it("should return undefined if no context anywhere", () => {
      expect(AsyncContext.fromArgs(["a", 1])).toBeUndefined();
    });

    it("should return undefined for empty args and no current", () => {
      expect(AsyncContext.fromArgs([])).toBeUndefined();
    });
  });

  describe("stripFromArgs", () => {
    it("should strip this exact instance from last arg", () => {
      const ctx = new AsyncContext().with(testKey, "strip");
      const args = ["a", 1, ctx];
      expect(ctx.stripFromArgs(args)).toEqual(["a", 1]);
    });

    it("should not strip a different AsyncContext instance", () => {
      const ctx1 = new AsyncContext().with(testKey, "one");
      const ctx2 = new AsyncContext().with(testKey, "two");
      const args = ["a", 1, ctx2];
      expect(ctx1.stripFromArgs(args)).toEqual(["a", 1, ctx2]);
    });

    it("should return args unchanged if last arg is not this", () => {
      const ctx = new AsyncContext();
      const args = ["a", 1, "b"];
      expect(ctx.stripFromArgs(args)).toBe(args);
    });

    it("should handle empty args", () => {
      const ctx = new AsyncContext();
      expect(ctx.stripFromArgs([])).toEqual([]);
    });
  });
});
