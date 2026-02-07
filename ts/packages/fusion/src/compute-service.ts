import { AsyncContext } from "@actuallab/core";
import type { ServiceDef, MethodDef } from "./service-def.js";
import { ComputeServiceInterceptor } from "./compute-service-interceptor.js";
import type { ComputeFn } from "./compute-function.js";
import type { Handler } from "./interceptor.js";

/** Creates a local compute service proxy — intercepts method calls for caching and dependency tracking. */
export function createLocalService<T extends Record<string, ComputeFn>>(
  serviceDef: ServiceDef,
  impl: T,
): T {
  const interceptor = new ComputeServiceInterceptor(serviceDef.name, impl);

  return new Proxy(impl, {
    get(target, prop, receiver) {
      if (typeof prop !== "string") return Reflect.get(target, prop, receiver);

      const methodDef = serviceDef.methods.get(prop);
      if (methodDef === undefined) return Reflect.get(target, prop, receiver);

      return (...actualArgs: unknown[]) => {
        // Resolve AsyncContext from last argument if provided
        const ctx = resolveAsyncContext(methodDef, actualArgs);
        const args = actualArgs.slice(0, methodDef.argCount);

        const invocation = { service: target, methodDef, args };

        const handler: Handler | null = interceptor.selectHandler(invocation);
        if (handler !== null) {
          // Run with the resolved context if available
          if (ctx !== undefined) {
            return ctx.run(() => handler(invocation));
          }
          return handler(invocation);
        }

        // Non-compute methods — call directly
        const fn = target[prop];
        if (fn !== undefined) return fn.call(target, ...args);
        throw new Error(`Method ${prop} not found on service ${serviceDef.name}`);
      };
    },
  }) as T;
}

function resolveAsyncContext(
  methodDef: MethodDef,
  actualArgs: unknown[],
): AsyncContext | undefined {
  if (actualArgs.length > methodDef.argCount) {
    const last = actualArgs[methodDef.argCount];
    if (last instanceof AsyncContext) return last;
  }
  return AsyncContext.current;
}
