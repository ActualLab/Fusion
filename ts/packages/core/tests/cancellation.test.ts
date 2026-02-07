import { describe, it, expect } from "vitest";
import {
  CancellationToken,
  CancellationTokenSource,
  CancellationError,
  cancellationTokenKey,
  AsyncContext,
} from "../src/index.js";

describe("CancellationToken", () => {
  it("none should not be cancelled", () => {
    expect(CancellationToken.none.isCancelled).toBe(false);
  });

  it("cancelled should be cancelled", () => {
    expect(CancellationToken.cancelled.isCancelled).toBe(true);
  });

  it("throwIfCancelled should not throw when not cancelled", () => {
    expect(() => CancellationToken.none.throwIfCancelled()).not.toThrow();
  });

  it("throwIfCancelled should throw CancellationError when cancelled", () => {
    expect(() => CancellationToken.cancelled.throwIfCancelled()).toThrow(CancellationError);
  });

  it("onCancelled should invoke callback immediately if already cancelled", () => {
    let called = false;
    CancellationToken.cancelled.onCancelled(() => { called = true; });
    expect(called).toBe(true);
  });

  it("onCancelled disposable should unregister callback", () => {
    const cts = new CancellationTokenSource();
    let called = false;
    const d = cts.token.onCancelled(() => { called = true; });
    d.dispose();
    cts.cancel();
    expect(called).toBe(false);
  });
});

describe("CancellationTokenSource", () => {
  it("should cancel the token", () => {
    const cts = new CancellationTokenSource();
    expect(cts.token.isCancelled).toBe(false);
    cts.cancel();
    expect(cts.token.isCancelled).toBe(true);
  });

  it("should invoke onCancelled callbacks", () => {
    const cts = new CancellationTokenSource();
    let called = false;
    cts.token.onCancelled(() => { called = true; });
    cts.cancel();
    expect(called).toBe(true);
  });

  it("should be idempotent", () => {
    const cts = new CancellationTokenSource();
    let count = 0;
    cts.token.onCancelled(() => count++);
    cts.cancel();
    cts.cancel();
    expect(count).toBe(1);
  });
});

describe("cancellationTokenKey", () => {
  it("should default to CancellationToken.none", () => {
    const ctx = new AsyncContext();
    expect(ctx.get(cancellationTokenKey)).toBe(CancellationToken.none);
  });

  it("should carry token through context", () => {
    const cts = new CancellationTokenSource();
    const ctx = new AsyncContext().with(cancellationTokenKey, cts.token);
    expect(ctx.get(cancellationTokenKey)).toBe(cts.token);
  });
});
