import { describe, it, expect } from "vitest";
import { nextVersion } from "../src/index.js";

describe("nextVersion", () => {
  it("should return monotonically increasing values", () => {
    const a = nextVersion();
    const b = nextVersion();
    const c = nextVersion();
    expect(b).toBe(a + 1);
    expect(c).toBe(b + 1);
  });

  it("should never return 0", () => {
    expect(nextVersion()).toBeGreaterThan(0);
  });
});
