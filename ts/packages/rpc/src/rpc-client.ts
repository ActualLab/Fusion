import type { RpcPeer } from "./rpc-peer.js";
import type { RpcServiceDef } from "./rpc-service-def.js";

/** Creates a typed RPC client proxy — intercepts method calls and sends them over RPC. */
export function createRpcClient<T extends object>(
  peer: RpcPeer,
  serviceDef: RpcServiceDef,
): T {
  return new Proxy({} as T, {
    get(_target, prop) {
      if (typeof prop !== "string") return undefined;

      const methodDef = serviceDef.methods.get(prop);
      if (methodDef === undefined) return undefined;

      return async (...args: unknown[]) => {
        // Strip AsyncContext if passed as last arg beyond declared count
        const callArgs = args.slice(0, methodDef.argCount);
        const wireMethod = `${serviceDef.name}.${prop}`;
        const outboundCall = peer.call(wireMethod, callArgs, methodDef.compute);
        return outboundCall.result.promise;
      };
    },
  });
}
