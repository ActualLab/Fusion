import {
  RpcHub,
  RpcServerPeer,
  type RpcServiceDef,
  type RpcDispatchContext,
  type WebSocketLike,
} from "@actuallab/rpc";
import { ComputeServiceHost } from "./compute-service-host.js";
import type { ComputeFn } from "./compute-function.js";

/** Extends RpcHub with Fusion compute service hosting — tracks compute calls and sends $sys-c.Invalidate. */
export class FusionHub {
  readonly rpcHub: RpcHub;
  private _computeHosts = new Map<string, ComputeServiceHost>();

  constructor(hubId?: string) {
    this.rpcHub = new RpcHub(hubId);
  }

  registerComputeService(
    serviceDef: RpcServiceDef,
    impl: Record<string, ComputeFn>,
  ): void {
    const host = new ComputeServiceHost(serviceDef, impl);
    this._computeHosts.set(serviceDef.name, host);

    // Build an RPC service impl that routes compute methods through ComputeServiceHost
    const rpcImpl: Record<string, (...args: unknown[]) => unknown> = {};
    for (const [methodName] of serviceDef.methods) {
      const fn = impl[methodName];
      if (fn === undefined) continue;

      if (host.isComputeMethod(methodName)) {
        // Compute method — use ComputeServiceHost for caching + invalidation
        rpcImpl[methodName] = async (...allArgs: unknown[]) => {
          // Last arg may be the RpcDispatchContext injected by RpcServiceHost
          const lastArg = allArgs[allArgs.length - 1];
          const isContext = lastArg != null
            && typeof lastArg === "object"
            && "__rpcDispatch" in lastArg;
          const context = isContext ? (lastArg as RpcDispatchContext) : undefined;
          const args = isContext ? allArgs.slice(0, -1) : allArgs;

          if (context !== undefined) {
            return host.handleComputeCall(methodName, args, context.callId, context.connection);
          }
          // No context — plain call without invalidation tracking
          return fn(...args);
        };
      } else {
        rpcImpl[methodName] = fn;
      }
    }

    this.rpcHub.registerService(serviceDef, rpcImpl);
  }

  registerPlainService(
    serviceDef: RpcServiceDef,
    impl: Record<string, (...args: unknown[]) => unknown>,
  ): void {
    this.rpcHub.registerService(serviceDef, impl);
  }

  acceptConnection(ws: WebSocketLike): RpcServerPeer {
    const peerId = `server-peer-${crypto.randomUUID()}`;
    const peer = new RpcServerPeer(peerId, this.rpcHub, ws);
    this.rpcHub.addPeer(peer);
    return peer;
  }

  close(): void {
    this.rpcHub.close();
  }
}
