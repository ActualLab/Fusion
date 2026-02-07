import { Interceptor, type Handler } from "./interceptor.js";
import { ComputeFunction, type ComputeFn } from "./compute-function.js";
import type { MethodDef } from "./service-def.js";

/** Intercepts compute method calls and routes them through ComputeFunction for caching and dependency tracking. */
export class ComputeServiceInterceptor extends Interceptor {
  private _serviceId: string;
  private _impl: Record<string, ComputeFn>;

  constructor(serviceId: string, impl: Record<string, ComputeFn>) {
    super();
    this._serviceId = serviceId;
    this._impl = impl;
  }

  protected createHandler(methodDef: MethodDef): Handler | null {
    if (!methodDef.compute) return null;

    const fn = this._impl[methodDef.name];
    if (fn === undefined) return null;

    const computeFn = new ComputeFunction(this._serviceId, methodDef.name, fn);

    return async (invocation) => {
      const computed = await computeFn.invoke(invocation.args);
      return computed.value;
    };
  }
}
