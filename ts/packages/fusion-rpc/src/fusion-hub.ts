import {
  RpcHub,
  RpcServerPeer,
  RpcWebSocketConnection,
  RpcOutboundComputeCall,
  getServiceMeta,
  getMethodsMeta,
  type RpcConnection,
  type WebSocketLike,
  type RpcServiceDef,
  type RpcMethodDef,
  type RpcServiceImpl,
  type RpcDispatchContext,
  type RpcPeer,
} from "@actuallab/rpc";
import {
  ComputeFunction,
  ComputeContext,
  type ComputeFunctionImpl,
} from "@actuallab/fusion";
import { AsyncContext } from "@actuallab/core";

/** Builds an RpcServiceDef from decorator metadata on a contract class. */
function buildServiceDefFromContract(
  contractClass: abstract new (...args: any[]) => any,
): RpcServiceDef {
  const svcMeta = getServiceMeta(contractClass);
  if (svcMeta === undefined) throw new Error("Contract class missing @rpcService metadata");

  const methodsMeta = getMethodsMeta(contractClass) ?? {};
  const methods = new Map<string, RpcMethodDef>();

  for (const [name, meta] of Object.entries(methodsMeta)) {
    methods.set(name, {
      name,
      serviceName: svcMeta.name,
      argCount: meta.argCount,
      compute: meta.compute ?? false,
      stream: meta.stream ?? false,
      noWait: meta.noWait ?? false,
    });
  }

  return { name: svcMeta.name, methods, compute: true };
}

/** Central coordinator for Fusion + RPC — manages compute services, invalidation wiring. */
export class FusionHub extends RpcHub {
  /** Convenience: register a compute service from decorator metadata on its contract class. */
  addServiceFromContract<T extends object>(
    contractClass: abstract new (...args: any[]) => any,
    impl: T,
  ): void {
    const def = buildServiceDefFromContract(contractClass);
    this.addService(def, impl as unknown as RpcServiceImpl);
  }

  /** Accept an incoming WebSocket and create a server peer. */
  acceptConnection(ws: WebSocketLike): RpcServerPeer {
    const peerId = crypto.randomUUID();
    const conn = new RpcWebSocketConnection(ws);
    const peer = new RpcServerPeer(peerId, this, conn);
    this.addPeer(peer);
    return peer;
  }

  /** Accept an RpcConnection and create a server peer. */
  acceptRpcConnection(conn: RpcConnection): RpcServerPeer {
    const peerId = crypto.randomUUID();
    const peer = new RpcServerPeer(peerId, this, conn);
    this.addPeer(peer);
    return peer;
  }

  protected override _wrapComputeServerMethod(
    _def: RpcServiceDef, methodName: string, _methodDef: RpcMethodDef,
    fn: (...args: unknown[]) => unknown, impl: object,
  ): (...args: unknown[]) => unknown {
    // Route through ComputeFunction + wire invalidation → $sys-c.Invalidate
    const cf = new ComputeFunction(methodName, fn as ComputeFunctionImpl);
    return async (...args: unknown[]) => {
      const context = extractDispatchContext(args);
      const cleanArgs = context !== undefined ? args.slice(0, -1) : args;

      const computed = await cf.invoke(impl, cleanArgs);

      // Wire invalidation → send $sys-c.Invalidate to the client
      if (context !== undefined) {
        computed.onInvalidated.add(() => {
          this.systemCallSender.invalidate(context.connection, context.callId);
        });
      }

      return computed.value;
    };
  }

  protected override _createComputeClientMethod(
    peer: RpcPeer, def: RpcServiceDef, methodName: string, methodDef: RpcMethodDef,
  ): (...args: unknown[]) => unknown {
    const wireMethod = `${def.name}.${methodName}`;
    // Local ComputeFunction whose impl makes an RPC call
    const rpcImpl: ComputeFunctionImpl = async function(...args: unknown[]) {
      const outboundCall = peer.call(wireMethod, args, true) as unknown as RpcOutboundComputeCall;
      const value = await outboundCall.result.promise;
      // Wire server invalidation → local computed invalidation
      const computeCtx = ComputeContext.from(AsyncContext.current);
      if (computeCtx) {
        outboundCall.whenInvalidated.promise.then(() => computeCtx.computed.invalidate());
      }
      return value;
    };
    const cf = new ComputeFunction(methodName, rpcImpl);
    const syntheticInstance = {};
    return (...args: unknown[]) => cf.invoke(syntheticInstance, args.slice(0, methodDef.argCount)).then(c => c.value);
  }
}

function extractDispatchContext(args: unknown[]): RpcDispatchContext | undefined {
  const last = args[args.length - 1];
  if (last !== null && typeof last === "object" && "__rpcDispatch" in last) {
    return last as RpcDispatchContext;
  }
  return undefined;
}
