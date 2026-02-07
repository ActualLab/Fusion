import { describe, it, expect } from "vitest";
import { DisposableBag } from "../src/index.js";

describe("DisposableBag", () => {
  it("should dispose items in reverse order", () => {
    const order: number[] = [];
    const bag = new DisposableBag();
    bag.add({ dispose: () => order.push(1) });
    bag.add({ dispose: () => order.push(2) });
    bag.add({ dispose: () => order.push(3) });
    bag.dispose();
    expect(order).toEqual([3, 2, 1]);
  });

  it("should be idempotent", () => {
    let count = 0;
    const bag = new DisposableBag();
    bag.add({ dispose: () => count++ });
    bag.dispose();
    bag.dispose();
    expect(count).toBe(1);
  });

  it("should throw when adding to disposed bag", () => {
    const bag = new DisposableBag();
    bag.dispose();
    expect(() => bag.add({ dispose: () => {} })).toThrow();
  });

  it("should support async disposal", async () => {
    const order: number[] = [];
    const bag = new DisposableBag();
    bag.add({ disposeAsync: async () => { order.push(1); } });
    bag.add({ dispose: () => { order.push(2); } });
    await bag.disposeAsync();
    expect(order).toEqual([2, 1]);
  });
});
