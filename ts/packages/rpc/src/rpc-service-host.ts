// .NET counterparts:
//   RpcServiceBase (17 lines) — abstract base class providing DI access (Services,
//     Hub, Log).  All RPC service implementations inherit from it.
//   RpcServiceRegistry — builds the full map of all services and their method defs
//     at startup; provides GetServerMethodResolver() for version-aware method
//     lookup.  Inbound call dispatch is handled by RpcInboundCall.Process() which
//     invokes the method via a compiled delegate chain (MethodDef.InboundCallInvoker).
//
// Omitted from .NET:
//   - Compiled delegate pipeline — .NET pre-compiles per-method invoker delegates
//     (InboundCallServerInvokerFactory<T>, InboundCallMiddlewareInvokerFactory<T>)
//     using generic type instantiation.  These invokers handle deserialization,
//     middleware chain, server method invocation, and result serialization — all
//     as a strongly-typed Task<TResult>.  TS uses a simple async function dispatch
//     via fn(...args) because there's no middleware pipeline and JSON handles any
//     type.
//   - ServiceResolver (DI-based lazy service resolution) — .NET resolves the
//     service implementation from DI on first use.  TS receives the impl object
//     directly in register().
//   - RpcServiceMode (Local/Client/Server/Hybrid) — .NET classifies each service
//     registration.  TS has no mode; services are either registered (server) or
//     proxied (client).
//   - Version-aware method resolution (ServerMethodResolver with legacy names) —
//     .NET can resolve methods by legacy names for rolling-upgrade compatibility.
//     TS has no versioning.
//   - RpcInboundContext / AsyncLocal — .NET sets thread-local inbound context
//     so system call handlers can access the current peer/message.  TS passes
//     context explicitly as a function argument (RpcDispatchContext).

import type { RpcMethodDef } from "./rpc-service-def.js";
import { wireMethodName } from "./rpc-service-def.js";
import type { RpcServiceDef } from "./rpc-service-def.js";
import type { RpcConnection } from "./rpc-connection.js";

export type RpcServiceImpl = Record<string, (...args: unknown[]) => unknown>;

/** Context passed to service dispatch — carries callId and connection for compute tracking. */
export interface RpcDispatchContext {
  __rpcDispatch: true;
  callId: number;
  connection: RpcConnection;
}

/** Dispatches inbound RPC calls to registered service implementations. */
export class RpcServiceHost {
  private _methods = new Map<string, { def: RpcMethodDef; fn: (...args: unknown[]) => unknown }>();

  register(def: RpcServiceDef, impl: RpcServiceImpl): void {
    for (const methodDef of def.methods.values()) {
      const fn = impl[methodDef.name];
      if (!fn) continue;
      this._methods.set(wireMethodName(methodDef), { def: methodDef, fn });
    }
  }

  async dispatch(wireMethod: string, args: unknown[], context?: RpcDispatchContext): Promise<unknown> {
    const entry = this._methods.get(wireMethod);
    if (entry === undefined) throw new Error(`Method not found: ${wireMethod}`);

    // Pass context as the last arg only for custom call types (e.g., compute) — their
    // wrapped functions use it; regular methods should not see it to avoid polluting ...args.
    if (context !== undefined && entry.def.callTypeId !== 0) {
      return await entry.fn(...args, context);
    }
    return await entry.fn(...args);
  }

  getMethodDef(wireMethod: string): RpcMethodDef | undefined {
    return this._methods.get(wireMethod)?.def;
  }
}
