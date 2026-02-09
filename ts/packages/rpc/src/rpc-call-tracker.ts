import { PromiseSource } from "@actuallab/core";

/** Tracks a pending outbound RPC call. */
export class RpcOutboundCall {
  readonly callId: number;
  readonly method: string;
  readonly result = new PromiseSource<unknown>();

  constructor(callId: number, method: string) {
    this.callId = callId;
    this.method = method;
  }
}

/** Tracks a pending outbound compute RPC call — adds invalidation notification. */
export class RpcOutboundComputeCall extends RpcOutboundCall {
  readonly whenInvalidated = new PromiseSource<void>();
}

/** Manages outbound calls by their RelatedId. */
export class RpcOutboundCallTracker {
  private _calls = new Map<number, RpcOutboundCall>();
  private _nextId = 1;

  get size(): number {
    return this._calls.size;
  }

  nextId(): number {
    return this._nextId++;
  }

  register(call: RpcOutboundCall): void {
    this._calls.set(call.callId, call);
  }

  get(callId: number): RpcOutboundCall | undefined {
    return this._calls.get(callId);
  }

  remove(callId: number): RpcOutboundCall | undefined {
    const call = this._calls.get(callId);
    if (call !== undefined) this._calls.delete(callId);
    return call;
  }

  activeCallIds(): number[] {
    return [...this._calls.keys()];
  }

  /** Reject all pending calls with the given error and resolve compute invalidations. */
  rejectAll(error: Error): void {
    for (const call of this._calls.values()) {
      call.result.reject(error);
      if (call instanceof RpcOutboundComputeCall)
        call.whenInvalidated.resolve();
    }
    this._calls.clear();
  }

  clear(): void {
    this._calls.clear();
  }
}

/** Tracks an incoming inbound RPC call. */
export class RpcInboundCall {
  readonly callId: number;
  readonly method: string;
  readonly args: unknown[];

  constructor(callId: number, method: string, args: unknown[]) {
    this.callId = callId;
    this.method = method;
    this.args = args;
  }
}

/** Manages inbound calls by their RelatedId. */
export class RpcInboundCallTracker {
  private _calls = new Map<number, RpcInboundCall>();

  get size(): number {
    return this._calls.size;
  }

  register(call: RpcInboundCall): void {
    this._calls.set(call.callId, call);
  }

  get(callId: number): RpcInboundCall | undefined {
    return this._calls.get(callId);
  }

  remove(callId: number): RpcInboundCall | undefined {
    const call = this._calls.get(callId);
    if (call !== undefined) this._calls.delete(callId);
    return call;
  }

  clear(): void {
    this._calls.clear();
  }
}
