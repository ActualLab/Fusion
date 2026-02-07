import type { WebSocketLike } from "../src/index.js";

/** Creates a pair of connected mock WebSockets for testing. */
export function createMockWsPair(): [WebSocketLike, WebSocketLike] {
  const a = new MockWebSocket();
  const b = new MockWebSocket();
  a._remote = b;
  b._remote = a;
  // Simulate connection in next microtask
  queueMicrotask(() => {
    a._readyState = 1;
    b._readyState = 1;
    a.onopen?.(undefined);
    b.onopen?.(undefined);
  });
  return [a, b];
}

class MockWebSocket implements WebSocketLike {
  _readyState = 0; // CONNECTING
  _remote: MockWebSocket | undefined;

  onopen: ((ev: unknown) => void) | null = null;
  onmessage: ((ev: { data: unknown }) => void) | null = null;
  onclose: ((ev: { code: number; reason: string }) => void) | null = null;
  onerror: ((ev: unknown) => void) | null = null;

  get readyState(): number {
    return this._readyState;
  }

  send(data: string): void {
    if (this._readyState !== 1) throw new Error("WebSocket is not open");
    // Deliver to remote side in next microtask
    queueMicrotask(() => {
      this._remote?.onmessage?.({ data });
    });
  }

  close(code?: number, reason?: string): void {
    if (this._readyState >= 2) return;
    this._readyState = 3;
    queueMicrotask(() => {
      this.onclose?.({ code: code ?? 1000, reason: reason ?? "" });
      if (this._remote !== undefined && this._remote._readyState < 2) {
        this._remote._readyState = 3;
        this._remote.onclose?.({ code: code ?? 1000, reason: reason ?? "" });
      }
    });
  }
}
