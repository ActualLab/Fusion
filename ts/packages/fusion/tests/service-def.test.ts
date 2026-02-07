import { describe, it, expect } from "vitest";
import { defineComputeService, defineRpcService, RpcType } from "../src/index.js";

describe("defineComputeService", () => {
  it("should create service def with compute methods", () => {
    const def = defineComputeService("ProductService", {
      getProduct: { args: [""], returns: RpcType.object, compute: true },
      getProducts: { args: ["", 0], returns: RpcType.stream },
    });

    expect(def.name).toBe("ProductService");
    expect(def.compute).toBe(true);

    const getProduct = def.methods.get("getProduct");
    expect(getProduct).toBeDefined();
    expect(getProduct?.argCount).toBe(1);
    expect(getProduct?.compute).toBe(true);
    expect(getProduct?.stream).toBe(false);

    const getProducts = def.methods.get("getProducts");
    expect(getProducts).toBeDefined();
    expect(getProducts?.argCount).toBe(2);
    expect(getProducts?.compute).toBe(true); // default from defineComputeService
    expect(getProducts?.stream).toBe(true);  // inferred from RpcType.stream
  });
});

describe("defineRpcService", () => {
  it("should create service def without compute by default", () => {
    const def = defineRpcService("PlainService", {
      doStuff: { args: [{}] },
    });

    expect(def.compute).toBe(false);
    const doStuff = def.methods.get("doStuff");
    expect(doStuff?.compute).toBe(false);
    expect(doStuff?.argCount).toBe(1);
  });
});
