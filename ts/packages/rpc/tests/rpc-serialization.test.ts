import { describe, it, expect } from "vitest";
import {
  serializeMessage,
  deserializeMessage,
  serializeFrame,
  splitFrame,
} from "../src/index.js";

describe("RPC Serialization", () => {
  it("should serialize message with no args", () => {
    const raw = serializeMessage({ Method: "$sys.KeepAlive" });
    expect(raw).toContain('"Method":"$sys.KeepAlive"');
    expect(raw.endsWith("\n")).toBe(true);
  });

  it("should serialize message with args", () => {
    const raw = serializeMessage(
      { Method: "Svc.get", RelatedId: 1 },
      ["hello", 42],
    );
    expect(raw).toContain('"Method":"Svc.get"');
    expect(raw).toContain('"RelatedId":1');
    // Args are JSON-encoded and separated by \x1F
    expect(raw).toContain('"hello"\x1F42');
  });

  it("should roundtrip message with args", () => {
    const original = { Method: "Svc.get", RelatedId: 5 };
    const args = ["hello", 42, { nested: true }];
    const serialized = serializeMessage(original, args);
    const { message, args: parsedArgs } = deserializeMessage(serialized);

    expect(message.Method).toBe("Svc.get");
    expect(message.RelatedId).toBe(5);
    expect(parsedArgs).toEqual(args);
  });

  it("should roundtrip message with no args", () => {
    const serialized = serializeMessage({ Method: "$sys.Cancel", RelatedId: 3 });
    const { message, args } = deserializeMessage(serialized);

    expect(message.Method).toBe("$sys.Cancel");
    expect(message.RelatedId).toBe(3);
    expect(args).toEqual([]);
  });

  it("should split and join frames", () => {
    const msgA = serializeMessage({ Method: "a" });
    const msgB = serializeMessage({ Method: "b" });
    const frame = serializeFrame([msgA, msgB]);
    const split = splitFrame(frame);

    expect(split.length).toBe(2);
    const first = split[0];
    const second = split[1];
    expect(first).toBeDefined();
    expect(second).toBeDefined();
    expect(deserializeMessage(first as string).message.Method).toBe("a");
    expect(deserializeMessage(second as string).message.Method).toBe("b");
  });
});
