import { describe, it, expect, beforeEach } from "vitest";
import { AsyncContext } from "@actuallab/core";
import {
  computeMethod,
  wrapComputeMethod,
  getMethodsMeta,
  computedRegistry,
  ComputedInput,
} from "../src/index.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

describe("@computeMethod decorator", () => {
  beforeEach(() => {
    computedRegistry.clear();
    AsyncContext.current = undefined;
  });

  it("should cache results for same args on same instance", async () => {
    let callCount = 0;

    class Svc {
      @computeMethod
      async getValue(id: string): Promise<number> {
        callCount++;
        return id.length;
      }
    }

    const svc = new Svc();
    const r1 = await svc.getValue("hello");
    const r2 = await svc.getValue("hello");

    expect(r1).toBe(5);
    expect(r2).toBe(5);
    expect(callCount).toBe(1); // cached
  });

  it("should produce different results for different args", async () => {
    class Svc {
      @computeMethod
      async getValue(id: string): Promise<number> {
        return id.length;
      }
    }

    const svc = new Svc();
    expect(await svc.getValue("ab")).toBe(2);
    expect(await svc.getValue("abc")).toBe(3);
  });

  it("should recompute after invalidation", async () => {
    let counter = 0;

    class Svc {
      @computeMethod
      async getCount(id: string): Promise<number> {
        return ++counter;
      }
    }

    const svc = new Svc();
    expect(await svc.getCount("x")).toBe(1);
    expect(await svc.getCount("x")).toBe(1); // cached

    svc.getCount.invalidate("x");

    expect(await svc.getCount("x")).toBe(2); // recomputed
  });

  it("should preserve 'this' binding", async () => {
    class Svc {
      private store = new Map<string, number>();

      @computeMethod
      async getValue(id: string): Promise<number> {
        return this.store.get(id) ?? 0;
      }

      setStore(id: string, value: number) {
        this.store.set(id, value);
      }
    }

    const svc = new Svc();
    svc.setStore("a", 42);
    expect(await svc.getValue("a")).toBe(42);
  });

  it("should keep different instances independent", async () => {
    class Svc {
      constructor(private prefix: string) {}

      @computeMethod
      async getValue(id: string): Promise<string> {
        return `${this.prefix}-${id}`;
      }
    }

    const svc1 = new Svc("A");
    const svc2 = new Svc("B");

    expect(await svc1.getValue("x")).toBe("A-x");
    expect(await svc2.getValue("x")).toBe("B-x");
  });

  it("should store metadata via Symbol.metadata", () => {
    class Svc {
      @computeMethod
      async getValue(id: string): Promise<number> { return 0; }

      @computeMethod
      async getOther(a: string, b: number): Promise<string> { return ""; }
    }

    const meta = getMethodsMeta(Svc);
    expect(meta).toBeDefined();
    expect(meta!["getValue"]).toEqual({ compute: true, argCount: 1 });
    expect(meta!["getOther"]).toEqual({ compute: true, argCount: 2 });
  });

  it("should handle errors in compute methods", async () => {
    class Svc {
      @computeMethod
      async getValue(id: string): Promise<number> {
        throw new Error("compute error");
      }
    }

    const svc = new Svc();
    await expect(svc.getValue("x")).rejects.toThrow("compute error");
  });
});

describe("wrapComputeMethod", () => {
  beforeEach(() => {
    computedRegistry.clear();
    AsyncContext.current = undefined;
  });

  it("should cache standalone function results", async () => {
    let callCount = 0;
    const computeValue = wrapComputeMethod((id: string) => {
      callCount++;
      return id.length;
    });

    expect(await computeValue("hello")).toBe(5);
    expect(await computeValue("hello")).toBe(5);
    expect(callCount).toBe(1);
  });

  it("should support invalidation", async () => {
    let counter = 0;
    const computeValue = wrapComputeMethod((id: string) => {
      return ++counter;
    });

    expect(await computeValue("x")).toBe(1);

    computeValue.invalidate("x");

    expect(await computeValue("x")).toBe(2);
  });
});
