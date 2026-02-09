const METHODS_META = Symbol.for("actuallab.methods");
const SERVICE_META = Symbol.for("actuallab.service");

export interface MethodMeta {
  argCount: number;
  compute?: boolean;
  stream?: boolean;
  noWait?: boolean;
}

export interface ServiceMeta {
  name: string;
}

/** Read method metadata from a decorated class. */
export function getMethodsMeta(cls: abstract new (...args: any[]) => any): Record<string, MethodMeta> | undefined {
  return (cls as any)[Symbol.metadata]?.[METHODS_META];
}

/** Read service metadata from a decorated class. */
export function getServiceMeta(cls: abstract new (...args: any[]) => any): ServiceMeta | undefined {
  return (cls as any)[Symbol.metadata]?.[SERVICE_META];
}

/** Class decorator — stores the RPC service wire name. */
export function rpcService(serviceName: string) {
  return function<T extends abstract new (...args: any[]) => any>(
    _target: T,
    context: ClassDecoratorContext<T>,
  ): void {
    const meta: ServiceMeta = ((context.metadata as any)[SERVICE_META] ??= {} as ServiceMeta);
    meta.name = serviceName;
  };
}

/** Method decorator — stores RPC method metadata (argCount, stream, noWait). Does NOT wrap the method. */
export function rpcMethod(options?: { stream?: boolean; noWait?: boolean }) {
  return function<This, Args extends unknown[], Return>(
    target: (this: This, ...args: Args) => Return,
    context: ClassMethodDecoratorContext<This, (this: This, ...args: Args) => Return>,
  ): (this: This, ...args: Args) => Return {
    const methodName = String(context.name);
    const methods: Record<string, MethodMeta> = ((context.metadata as any)[METHODS_META] ??= {});
    methods[methodName] = {
      ...methods[methodName],
      argCount: target.length,
      stream: options?.stream ?? false,
      noWait: options?.noWait ?? false,
    };
    return target; // no wrapping — RPC proxy handles invocation
  };
}
