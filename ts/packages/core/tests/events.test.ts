import { describe, it, expect } from "vitest";
import { EventHandlerSet } from "../src/index.js";

describe("EventHandlerSet", () => {
  it("should trigger all handlers", () => {
    const events = new EventHandlerSet<number>();
    const values: number[] = [];
    events.add((v) => values.push(v));
    events.add((v) => values.push(v * 10));
    events.trigger(3);
    expect(values).toEqual([3, 30]);
  });

  it("should remove handlers", () => {
    const events = new EventHandlerSet<number>();
    const values: number[] = [];
    const handler = (v: number) => values.push(v);
    events.add(handler);
    events.trigger(1);
    events.remove(handler);
    events.trigger(2);
    expect(values).toEqual([1]);
  });

  it("should track count", () => {
    const events = new EventHandlerSet<void>();
    expect(events.count).toBe(0);
    const handler = () => {};
    events.add(handler);
    expect(events.count).toBe(1);
    events.remove(handler);
    expect(events.count).toBe(0);
  });

  it("whenNext should resolve on next trigger", async () => {
    const events = new EventHandlerSet<string>();
    const promise = events.whenNext();
    events.trigger("hello");
    expect(await promise).toBe("hello");
  });

  it("whenNext should auto-remove handler after firing", async () => {
    const events = new EventHandlerSet<string>();
    const promise = events.whenNext();
    expect(events.count).toBe(1);
    events.trigger("hello");
    await promise;
    expect(events.count).toBe(0);
  });
});
