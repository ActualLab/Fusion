import { ComputeFunction } from "./compute-function.js";
import { computedRegistry } from "./computed-registry.js";

const METHODS_META = Symbol.for("actuallab.methods");

export interface MethodMeta {
  argCount: number;
  compute?: boolean;
  stream?: boolean;
}

/** Read method metadata from a decorated class. */
export function getMethodsMeta(cls: abstract new (...args: any[]) => any): Record<string, MethodMeta> | undefined {
  return (cls as any)[Symbol.metadata]?.[METHODS_META];
}

/** Method decorator — wraps a method to route through ComputeFunction for caching and dependency tracking. */
export function computeMethod<This, Args extends unknown[], Return>(
  target: (this: This, ...args: Args) => Return,
  context: ClassMethodDecoratorContext<This, (this: This, ...args: Args) => Return>,
): (this: This, ...args: Args) => Return {
  const methodName = String(context.name);

  // Store metadata
  const methods: Record<string, MethodMeta> = ((context.metadata as any)[METHODS_META] ??= {});
  methods[methodName] = { ...methods[methodName], compute: true, argCount: target.length };

  // ONE ComputeFunction per class×method — created at decoration time
  const cf = new ComputeFunction(methodName, target as any);

  // Prototype-level replacement — unwraps Computed to return the value directly
  const replacement = function(this: This, ...allArgs: Args): Return {
    return cf.invoke(this as object, allArgs).then(c => c.value) as Return;
  };

  // Per-instance setup: create bound method with .invalidate pre-bound
  context.addInitializer(function(this: This) {
    const instance = this;
    const boundMethod = (...allArgs: unknown[]) => {
      return cf.invoke(instance as object, allArgs).then(c => c.value);
    };
    (boundMethod as any).invalidate = (...args: unknown[]) => {
      const key = cf.buildKey(instance as object, args);
      computedRegistry.get(key)?.invalidate();
    };
    (this as any)[methodName] = boundMethod;
  });

  return replacement;
}

/** Wrap a standalone function as a compute function with caching and .invalidate(). */
export function wrapComputeMethod<Args extends unknown[], Return>(
  fn: (...args: Args) => Return,
): ((...args: Args) => Promise<Return>) & { invalidate: (...args: Args) => void } {
  const syntheticInstance = {};
  const methodName = fn.name || "anonymous";
  const cf = new ComputeFunction(methodName, fn as any);

  const wrapped = (...allArgs: unknown[]) => {
    return cf.invoke(syntheticInstance, allArgs).then(c => c.value as Return);
  };

  (wrapped as any).invalidate = (...args: unknown[]) => {
    const key = cf.buildKey(syntheticInstance, args);
    computedRegistry.get(key)?.invalidate();
  };

  return wrapped as any;
}
