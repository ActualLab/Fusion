// .NET counterpart:
//   RpcMethodAttribute — optional attribute that can override a method's wire name.
//     Method metadata (arg count, return type, NoWait, stream, etc.) is discovered
//     automatically via reflection on the service interface.
//   Interface-based metadata — .NET discovers all methods by reflecting on the
//     service interface type.  NoWait is determined by return type == Task<RpcNoWait>.
//     Stream is determined by return type being IAsyncEnumerable<T>.
//
// Omitted from .NET:
//   - Reflection-based auto-discovery — .NET scans interface methods, parameter
//     types, and return types at startup.  TS uses explicit decorator metadata
//     because TypeScript erases type information at runtime.
//   - RpcMethodAttribute.Name — custom wire name override.  TS decorators don't
//     support custom wire names (always "ServiceName.methodName").
//   - RpcServiceAttribute — .NET has no class-level decorator; the service name
//     is configured in RpcServiceBuilder via DI.  TS uses @rpcService("name")
//     class decorator as a convenient alternative to DI configuration.
//   - Automatic NoWait detection from return type — .NET checks if return type
//     is Task<RpcNoWait>.  TS requires explicit { noWait: true } in @rpcMethod
//     because return types are erased.
//   - Automatic stream detection from return type — .NET checks for
//     IAsyncEnumerable<T>.  TS requires explicit { returns: RpcType.stream }.
//   - compute flag in method metadata — stored here for FusionHub's
//     addServiceFromContract() which reads decorator metadata to build a
//     RpcServiceDef.  .NET determines this via the service interface hierarchy
//     (IComputeService marker interface).

const METHODS_META = Symbol.for("actuallab.methods");
const SERVICE_META = Symbol.for("actuallab.service");

export interface MethodMeta {
  argCount: number;
  returns?: symbol;
  noWait?: boolean;
  [key: symbol]: unknown;
}

export interface ServiceMeta {
  name: string;
  ctOffset?: number;
}

/** Read method metadata from a decorated class. */
export function getMethodsMeta(cls: abstract new (...args: any[]) => any): Record<string, MethodMeta> | undefined {
  return (cls as any)[Symbol.metadata]?.[METHODS_META];
}

/** Read service metadata from a decorated class. */
export function getServiceMeta(cls: abstract new (...args: any[]) => any): ServiceMeta | undefined {
  return (cls as any)[Symbol.metadata]?.[SERVICE_META];
}

/** Class decorator — stores the RPC service wire name and optional ctOffset. */
export function rpcService(serviceName: string, options?: { ctOffset?: number }) {
  return function<T extends abstract new (...args: any[]) => any>(
    _target: T,
    context: ClassDecoratorContext<T>,
  ): void {
    const meta: ServiceMeta = ((context.metadata as any)[SERVICE_META] ??= {} as ServiceMeta);
    meta.name = serviceName;
    if (options?.ctOffset !== undefined)
      meta.ctOffset = options.ctOffset;
  };
}

/** Method decorator — stores RPC method metadata (argCount, returnType, noWait). Does NOT wrap the method. */
export function rpcMethod(options?: { returns?: symbol; noWait?: boolean }) {
  return function<This, Args extends unknown[], Return>(
    target: (this: This, ...args: Args) => Return,
    context: ClassMethodDecoratorContext<This, (this: This, ...args: Args) => Return>,
  ): (this: This, ...args: Args) => Return {
    const methodName = String(context.name);
    const methods: Record<string, MethodMeta> = ((context.metadata as any)[METHODS_META] ??= {});
    methods[methodName] = {
      ...methods[methodName],
      argCount: target.length,
      returns: options?.returns,
      noWait: options?.noWait ?? false,
    };
    return target; // no wrapping — RPC proxy handles invocation
  };
}
