import {
  RpcHub,
  RpcServerPeer,
  RpcSystemCalls,
  getServiceMeta,
  getMethodsMeta,
  serializeMessage,
  type WebSocketLike,
  type RpcServiceDef,
  type RpcServiceImpl,
  type RpcDispatchContext,
} from "@actuallab/rpc";
import {
  ComputeFunction,
  type ComputeFunctionImpl,
  type MethodMeta,
} from "@actuallab/fusion";

interface RegisteredComputeMethod {
  cf: ComputeFunction;
  instance: object;
}

/** Central coordinator for Fusion + RPC — manages compute services, invalidation wiring. */
export class FusionHub extends RpcHub {
  private _computeMethods = new Map<string, RegisteredComputeMethod>();

  /** Register a compute service using decorator metadata from its contract class. */
  registerComputeService<T extends object>(
    contractClass: abstract new (...args: any[]) => any,
    impl: T,
  ): void {
    const svcMeta = getServiceMeta(contractClass);
    if (svcMeta === undefined) throw new Error("Contract class missing @rpcService metadata");

    const methodsMeta = getMethodsMeta(contractClass) ?? {};
    this._registerServiceImpl(svcMeta.name, methodsMeta, impl);
  }

  /** Register a compute service using an RpcServiceDef (legacy API). */
  registerComputeServiceDef(def: RpcServiceDef, impl: RpcServiceImpl): void {
    const methodsMeta: Record<string, MethodMeta> = {};
    for (const [name, methodDef] of def.methods) {
      methodsMeta[name] = {
        argCount: methodDef.argCount,
        compute: methodDef.compute,
        stream: methodDef.stream,
      };
    }
    this._registerServiceImpl(def.name, methodsMeta, impl as object);
  }

  /** Register a plain (non-compute) RPC service. */
  registerPlainService(def: RpcServiceDef, impl: RpcServiceImpl): void {
    this.serviceHost.register(def, impl);
  }

  /** Accept an incoming WebSocket and create a server peer. */
  acceptConnection(ws: WebSocketLike): RpcServerPeer {
    const peerId = crypto.randomUUID();
    const peer = new RpcServerPeer(peerId, this, ws);
    this.addPeer(peer);
    return peer;
  }

  private _registerServiceImpl(
    serviceName: string,
    methodsMeta: Record<string, MethodMeta>,
    impl: object,
  ): void {
    const rpcImpl: RpcServiceImpl = {};
    const methods = new Map<string, { name: string; serviceName: string; argCount: number; compute: boolean; stream: boolean }>();

    for (const [methodName, meta] of Object.entries(methodsMeta)) {
      const isCompute = meta.compute ?? false;
      const wireMethod = `${serviceName}.${methodName}`;

      methods.set(methodName, {
        name: methodName,
        serviceName,
        argCount: meta.argCount,
        compute: isCompute,
        stream: meta.stream ?? false,
      });

      if (isCompute) {
        // Create a ComputeFunction for this method
        const originalFn = (impl as any)[methodName] as ComputeFunctionImpl;
        if (typeof originalFn !== "function") {
          throw new Error(`Method ${wireMethod} not found on implementation`);
        }

        const cf = new ComputeFunction(methodName, originalFn);
        this._computeMethods.set(wireMethod, { cf, instance: impl });

        // Wrap for RPC dispatch: route through ComputeFunction, wire invalidation
        rpcImpl[methodName] = async (...args: unknown[]) => {
          const context = extractDispatchContext(args);
          const cleanArgs = context !== undefined ? args.slice(0, -1) : args;

          const computed = await cf.invoke(impl, cleanArgs);

          // Wire invalidation → send $sys-c.Invalidate to the client
          if (context !== undefined) {
            computed.onInvalidated.add(() => {
              const msg = serializeMessage({
                Method: RpcSystemCalls.invalidate,
                RelatedId: context.callId,
              });
              try { context.connection.send(msg); } catch { /* peer may be disconnected */ }
            });
          }

          return computed.value;
        };
      } else {
        // Plain method — pass through directly
        const fn = (impl as any)[methodName];
        if (typeof fn === "function") {
          rpcImpl[methodName] = fn.bind(impl);
        }
      }
    }

    // Register with the service host using a synthetic RpcServiceDef
    const serviceDef: RpcServiceDef = { name: serviceName, methods, compute: true };
    this.serviceHost.register(serviceDef, rpcImpl);
  }
}

function extractDispatchContext(args: unknown[]): RpcDispatchContext | undefined {
  const last = args[args.length - 1];
  if (last !== null && typeof last === "object" && "__rpcDispatch" in last) {
    return last as RpcDispatchContext;
  }
  return undefined;
}
