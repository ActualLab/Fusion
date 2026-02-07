import type { Invocation } from "./invocation.js";
import type { MethodDef } from "./service-def.js";

export type Handler = (invocation: Invocation) => unknown;

/** Base class for the interceptor chain — caches handlers per method and supports null-coalescing composition. */
export abstract class Interceptor {
  private _cache = new Map<string, Handler | null>();

  selectHandler(invocation: Invocation): Handler | null {
    const key = `${invocation.methodDef.serviceName}.${invocation.methodDef.name}`;
    let handler = this._cache.get(key);
    if (handler === undefined) {
      handler = this.createHandler(invocation.methodDef) ?? null;
      this._cache.set(key, handler);
    }
    return handler;
  }

  protected abstract createHandler(methodDef: MethodDef): Handler | null;
}
