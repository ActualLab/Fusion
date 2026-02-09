import type { RpcPeer } from "./rpc-peer.js";
import { RpcServiceHost, type RpcServiceImpl } from "./rpc-service-host.js";
import type { RpcServiceDef, RpcMethodDef } from "./rpc-service-def.js";
import { RpcSystemCallSender } from "./rpc-system-call-sender.js";

/** Central RPC coordinator — manages peers, services, and configuration. */
export class RpcHub {
  readonly hubId: string;
  readonly peers = new Map<string, RpcPeer>();
  readonly serviceHost: RpcServiceHost;
  readonly systemCallSender = new RpcSystemCallSender();

  constructor(hubId?: string) {
    this.hubId = hubId ?? crypto.randomUUID();
    this.serviceHost = new RpcServiceHost();
  }

  addPeer(peer: RpcPeer): void {
    this.peers.set(peer.id, peer);
  }

  removePeer(id: string): void {
    const peer = this.peers.get(id);
    if (peer !== undefined) {
      peer.close();
      this.peers.delete(id);
    }
  }

  getPeer(id: string): RpcPeer | undefined {
    return this.peers.get(id);
  }

  /** Register a service with optional compute method wrapping. */
  addService(def: RpcServiceDef, impl: RpcServiceImpl): void {
    const wrappedImpl: RpcServiceImpl = {};
    for (const [name, methodDef] of def.methods) {
      const fn = impl[name];
      if (!fn) continue;
      wrappedImpl[name] = methodDef.compute
        ? this._wrapComputeServerMethod(def, name, methodDef, fn, impl as object)
        : fn;
    }
    this.serviceHost.register(def, wrappedImpl);
  }

  /** Create a typed client proxy for a service on a remote peer. */
  addClient<T extends object>(peer: RpcPeer, def: RpcServiceDef): T {
    return new Proxy({} as T, {
      get: (_target, prop) => {
        if (typeof prop !== "string") return undefined;

        const methodDef = def.methods.get(prop);
        if (methodDef === undefined) return undefined;

        // NoWait methods
        if (methodDef.noWait) {
          return (...args: unknown[]) => {
            const callArgs = args.slice(0, methodDef.argCount);
            const wireMethod = `${def.name}.${prop}`;
            peer.callNoWait(wireMethod, callArgs);
          };
        }

        // Compute methods — allow subclass override
        if (methodDef.compute) {
          return this._createComputeClientMethod(peer, def, prop, methodDef);
        }

        // Regular methods
        return async (...args: unknown[]) => {
          const callArgs = args.slice(0, methodDef.argCount);
          const wireMethod = `${def.name}.${prop}`;
          const outboundCall = peer.call(wireMethod, callArgs, false);
          return outboundCall.result.promise;
        };
      },
    });
  }

  /** @deprecated Use addService instead. */
  registerService(def: RpcServiceDef, impl: RpcServiceImpl): void {
    this.serviceHost.register(def, impl);
  }

  close(): void {
    for (const peer of this.peers.values()) peer.close();
    this.peers.clear();
  }

  /** Override in FusionHub — default is pass-through. */
  protected _wrapComputeServerMethod(
    _def: RpcServiceDef, _methodName: string, _methodDef: RpcMethodDef,
    fn: (...args: unknown[]) => unknown, _impl: object,
  ): (...args: unknown[]) => unknown {
    return fn;
  }

  /** Override in FusionHub — default is regular compute RPC call. */
  protected _createComputeClientMethod(
    peer: RpcPeer, def: RpcServiceDef, methodName: string, methodDef: RpcMethodDef,
  ): (...args: unknown[]) => unknown {
    const wireMethod = `${def.name}.${methodName}`;
    return async (...args: unknown[]) => {
      const callArgs = args.slice(0, methodDef.argCount);
      const outboundCall = peer.call(wireMethod, callArgs, true);
      return outboundCall.result.promise;
    };
  }
}
