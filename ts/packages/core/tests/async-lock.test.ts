import { describe, it, expect } from "vitest";
import { AsyncLock } from "../src/index.js";

describe("AsyncLock", () => {
  it("should serialize access", async () => {
    const lock = new AsyncLock();
    const order: number[] = [];

    const task1 = lock.run(async () => {
      order.push(1);
      await new Promise((r) => setTimeout(r, 10));
      order.push(2);
    });

    const task2 = lock.run(async () => {
      order.push(3);
      order.push(4);
    });

    await Promise.all([task1, task2]);
    expect(order).toEqual([1, 2, 3, 4]);
  });

  it("should return the value from fn", async () => {
    const lock = new AsyncLock();
    const result = await lock.run(() => 42);
    expect(result).toBe(42);
  });

  it("should release lock on error", async () => {
    const lock = new AsyncLock();
    await expect(lock.run(() => { throw new Error("boom"); })).rejects.toThrow("boom");
    expect(lock.isLocked).toBe(false);
    // Should be able to acquire again
    const result = await lock.run(() => "ok");
    expect(result).toBe("ok");
  });

  it("should throw when releasing an unlocked lock", () => {
    const lock = new AsyncLock();
    expect(() => lock.release()).toThrow();
  });

  it("should report isLocked correctly", async () => {
    const lock = new AsyncLock();
    expect(lock.isLocked).toBe(false);
    await lock.acquire();
    expect(lock.isLocked).toBe(true);
    lock.release();
    expect(lock.isLocked).toBe(false);
  });
});
