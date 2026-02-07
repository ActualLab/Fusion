import type { RpcServiceDef, RpcMethodDef } from "./rpc-service-def.js";
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
  private _services = new Map<string, { def: RpcServiceDef; impl: RpcServiceImpl }>();

  register(def: RpcServiceDef, impl: RpcServiceImpl): void {
    this._services.set(def.name, { def, impl });
  }

  async dispatch(wireMethod: string, args: unknown[], context?: RpcDispatchContext): Promise<unknown> {
    // Wire method format: "ServiceName.methodName"
    const dotIndex = wireMethod.indexOf(".");
    if (dotIndex === -1) throw new Error(`Invalid method format: ${wireMethod}`);

    const serviceName = wireMethod.substring(0, dotIndex);
    const methodName = wireMethod.substring(dotIndex + 1);

    const entry = this._services.get(serviceName);
    if (entry === undefined) throw new Error(`Service not found: ${serviceName}`);

    const fn = entry.impl[methodName];
    if (fn === undefined) throw new Error(`Method not found: ${wireMethod}`);

    // Pass context as the last arg if present — compute hosts use this
    if (context !== undefined) {
      return await fn(...args, context);
    }
    return await fn(...args);
  }

  getMethodDef(wireMethod: string): RpcMethodDef | undefined {
    const dotIndex = wireMethod.indexOf(".");
    if (dotIndex === -1) return undefined;
    const serviceName = wireMethod.substring(0, dotIndex);
    const methodName = wireMethod.substring(dotIndex + 1);
    return this._services.get(serviceName)?.def.methods.get(methodName);
  }
}
