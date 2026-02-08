import { describe, it, expect } from "vitest";
import { rpcService, rpcMethod, getServiceMeta, getMethodsMeta } from "../src/index.js";

describe("@rpcService decorator", () => {
  it("should store service name in metadata", () => {
    @rpcService("ProductService")
    class ProductService {
      async getProduct(id: string): Promise<unknown> { return undefined; }
    }

    const meta = getServiceMeta(ProductService);
    expect(meta).toBeDefined();
    expect(meta!.name).toBe("ProductService");
  });
});

describe("@rpcMethod decorator", () => {
  it("should store method metadata", () => {
    class Svc {
      @rpcMethod()
      async getProduct(id: string): Promise<unknown> { return undefined; }

      @rpcMethod({ stream: true })
      async *getProducts(query: string, limit: number): AsyncGenerator<unknown> { yield undefined; }
    }

    const meta = getMethodsMeta(Svc);
    expect(meta).toBeDefined();

    expect(meta!["getProduct"]).toEqual({ argCount: 1, stream: false });
    expect(meta!["getProducts"]).toEqual({ argCount: 2, stream: true });
  });

  it("should not wrap the method", async () => {
    class Svc {
      @rpcMethod()
      async getItem(id: string): Promise<string> { return id; }
    }

    // rpcMethod returns target unchanged — method still works normally
    const svc = new Svc();
    expect(await svc.getItem("abc")).toBe("abc");

    const meta = getMethodsMeta(Svc);
    expect(meta).toBeDefined();
    expect(meta!["getItem"]).toEqual({ argCount: 1, stream: false });
  });
});

describe("@rpcService + @rpcMethod combined", () => {
  it("should store both service and method metadata on same class", () => {
    @rpcService("CounterService")
    class ICounterService {
      @rpcMethod()
      async getCount(key: string): Promise<number> { return 0; }

      @rpcMethod()
      async setCount(key: string, value: number): Promise<void> {}

      @rpcMethod({ stream: true })
      async *watchCount(key: string): AsyncGenerator<number> { yield 0; }
    }

    const svcMeta = getServiceMeta(ICounterService);
    expect(svcMeta).toEqual({ name: "CounterService" });

    const methods = getMethodsMeta(ICounterService);
    expect(methods).toBeDefined();
    expect(methods!["getCount"]).toEqual({ argCount: 1, stream: false });
    expect(methods!["setCount"]).toEqual({ argCount: 2, stream: false });
    expect(methods!["watchCount"]).toEqual({ argCount: 1, stream: true });
  });

  it("should return undefined for non-decorated classes", () => {
    class Plain {
      async doStuff(): Promise<void> {}
    }

    expect(getServiceMeta(Plain)).toBeUndefined();
    expect(getMethodsMeta(Plain)).toBeUndefined();
  });
});
