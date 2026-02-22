// .NET counterparts:
//   RpcComputeServiceDef — extends RpcServiceDef with ComputedOptionsProvider.
//   RpcComputeMethodDef — extends RpcMethodDef with ComputedOptions and sets
//     CallType = RpcComputeCallType (custom call type with its own
//     RpcOutboundComputeCall / RpcInboundComputeCall factories).
//   RpcInboundComputeCallHandler — an IRpcMiddleware that wraps inbound compute
//     calls in a ComputeContext, captures the Computed<T> instance, and stores
//     it on RpcInboundComputeCall.Computed for invalidation tracking.
//   RemoteComputeServiceInterceptor — the client-side interceptor that creates
//     compute-aware outbound calls, tracking server invalidation signals.
//
//   In .NET this is split across ~10 files using DI, middleware, interceptors,
//   and the RpcComputeCallType custom call type.  TS consolidates it into a
//   single FusionHub subclass with two method overrides.
//
// Omitted from .NET:
//   - RpcComputeCallType / typed RpcInboundComputeCall<T> / RpcOutboundComputeCall<T> —
//     .NET uses a separate call type with custom factories, result handling, and
//     stage tracking.  TS extends the base RpcOutboundComputeCall with a
//     whenInvalidated promise (sufficient for the TS use case).
//   - ComputedOptionsProvider / ComputedOptions per method — .NET resolves
//     per-method caching/invalidation options via DI.  TS uses default
//     ComputeFunction behavior for all methods.
//   - RpcInboundComputeCallHandler middleware — .NET wraps the inbound call in
//     ComputeContext using the middleware pipeline.  TS wraps it directly in
//     _wrapServerMethod by calling cf.invoke() (which sets up
//     ComputeContext internally).
//   - LocalExecutionMode / ConstrainedEntry — .NET controls whether compute calls
//     can execute locally or must go remote (for Fusion's consistency guarantees).
//     TS always executes locally (server-side) or remotely (client-side).
//   - Invalidation via RpcInboundComputeCall.Computed.Invalidated event — .NET
//     attaches an invalidation callback to the captured Computed<T> that calls
//     SystemCallSender.Complete(peer, call, invalidatedResult).  TS uses
//     $sys-c.Invalidate system call instead (equivalent wire effect).
//   - RpcOutboundComputeCall.SetError → SetInvalidatedUnsafe — .NET invalidates
//     the local computed when an error or reroute occurs.  TS resolves
//     whenInvalidated on disconnect via rejectAll().

import {
  RpcHub,
  RpcServerPeer,
  RpcWebSocketConnection,
  RpcSystemCallHandler,
  serializeMessage,
  defineRpcService,
  wireMethodName,
  RpcType,
  type RpcConnection,
  type WebSocketLike,
  type RpcServiceDef,
  type RpcMethodDef,
  type RpcMethodDefInput,
  type RpcServiceImpl,
  type RpcDispatchContext,
  type RpcPeer,
  type RpcMessage,
  type RpcCallOptions,
  getServiceMeta,
  getMethodsMeta,
} from "@actuallab/rpc";
import {
  ComputeFunction,
  ComputeContext,
  type ComputeFunctionImpl,
} from "@actuallab/fusion";
import { AsyncContext } from "@actuallab/core";
import { RpcOutboundComputeCall } from "./rpc-outbound-compute-call.js";

/** Call type ID for Fusion compute calls — matches .NET's RpcComputeCallType.Id. */
export const FUSION_CALL_TYPE_ID = 1;

/** Wire method name for invalidation system call. */
const FUSION_INVALIDATE_METHOD = "$sys-c.Invalidate:0";

/** Handles Fusion-specific system calls ($sys-c.Invalidate), delegating the rest to base. */
class FusionSystemCallHandler extends RpcSystemCallHandler {
  override handle(message: RpcMessage, args: unknown[], peer: RpcPeer): void {
    const method = message.Method;
    const relatedId = message.RelatedId ?? 0;

    if (method === FUSION_INVALIDATE_METHOD) {
      const call = peer.outbound.remove(relatedId);
      if (call instanceof RpcOutboundComputeCall) {
        call.whenInvalidated.resolve();
      }
      return;
    }

    super.handle(message, args, peer);
  }
}

/** Creates a compute service definition — all methods default to FUSION_CALL_TYPE_ID. */
export function defineComputeService(
  name: string,
  methods: Record<string, RpcMethodDefInput>,
): RpcServiceDef {
  const withCallType: Record<string, RpcMethodDefInput> = {};
  for (const [methodName, input] of Object.entries(methods)) {
    withCallType[methodName] = { ...input, callTypeId: input.callTypeId ?? FUSION_CALL_TYPE_ID };
  }
  return defineRpcService(name, withCallType, { ctOffset: 1 });
}

/** Central coordinator for Fusion + RPC — manages compute services, invalidation wiring. */
export class FusionHub extends RpcHub {
  constructor(hubId?: string) {
    super(hubId);
    this.systemCallHandler = new FusionSystemCallHandler();
  }

  /** Accept an incoming WebSocket and create a server peer. */
  acceptConnection(ws: WebSocketLike): RpcServerPeer {
    const ref = `server://${crypto.randomUUID()}`;
    const conn = new RpcWebSocketConnection(ws);
    const peer = this.getServerPeer(ref);
    peer.accept(conn);
    return peer;
  }

  /** Accept an RpcConnection and create a server peer. */
  acceptRpcConnection(conn: RpcConnection): RpcServerPeer {
    const ref = `server://${crypto.randomUUID()}`;
    const peer = this.getServerPeer(ref);
    peer.accept(conn);
    return peer;
  }

  /** Override to apply ctOffset=1 default and FUSION_CALL_TYPE_ID for compute methods. */
  protected override _buildServiceDef(cls: abstract new (...args: any[]) => any): RpcServiceDef {
    const svcMeta = getServiceMeta(cls);
    if (svcMeta === undefined) throw new Error("Contract class missing @rpcService metadata");

    const methodsMeta = getMethodsMeta(cls) ?? {};
    // FusionHub defaults ctOffset to 1 (CancellationToken convention)
    const ctOffset = svcMeta.ctOffset ?? 1;
    const methods = new Map<string, RpcMethodDef>();

    for (const [name, meta] of Object.entries(methodsMeta)) {
      const wireArgCount = meta.argCount + ctOffset;
      const mapKey = `${name}:${wireArgCount}`;
      methods.set(mapKey, {
        name,
        serviceName: svcMeta.name,
        argCount: meta.argCount,
        wireArgCount,
        callTypeId: (meta as any).compute === true ? FUSION_CALL_TYPE_ID : 0,
        stream: meta.returns === RpcType.stream,
        noWait: meta.noWait ?? false,
      });
    }

    return { name: svcMeta.name, methods };
  }

  protected override _wrapServerMethod(
    methodDef: RpcMethodDef,
    fn: (...args: unknown[]) => unknown, impl: object,
  ): (...args: unknown[]) => unknown {
    if (methodDef.callTypeId !== FUSION_CALL_TYPE_ID)
      return fn;

    // Route through ComputeFunction + wire invalidation → $sys-c.Invalidate
    const cf = new ComputeFunction(methodDef.name, fn as ComputeFunctionImpl);
    return async (...args: unknown[]) => {
      const context = extractDispatchContext(args);
      const cleanArgs = context !== undefined ? args.slice(0, -1) : args;

      const computed = await cf.invoke(impl, cleanArgs);

      // Wire invalidation → send $sys-c.Invalidate to the client
      if (context !== undefined) {
        computed.onInvalidated.add(() => {
          const msg = serializeMessage({
            Method: FUSION_INVALIDATE_METHOD,
            RelatedId: context.callId,
          });
          context.connection.send(msg);
        });
      }

      return computed.value;
    };
  }

  protected override _createClientMethod(
    peer: RpcPeer, methodDef: RpcMethodDef,
  ): (...args: unknown[]) => unknown {
    if (methodDef.callTypeId !== FUSION_CALL_TYPE_ID)
      return super._createClientMethod(peer, methodDef);

    const wireName = wireMethodName(methodDef);
    // Local ComputeFunction whose impl makes an RPC call
    const rpcImpl: ComputeFunctionImpl = async function(...args: unknown[]) {
      // Capture ComputeContext synchronously — AsyncContext.current is the child
      // context set by ComputeFunction.invoke() and won't survive the await below.
      const computeCtx = ComputeContext.from(AsyncContext.current);
      const callOptions: RpcCallOptions = {
        callTypeId: FUSION_CALL_TYPE_ID,
        outboundCallFactory: (id, m) => new RpcOutboundComputeCall(id, m),
      };
      const outboundCall = peer.call(wireName, args, callOptions) as RpcOutboundComputeCall;
      const value = await outboundCall.result.promise;
      // Wire server invalidation → local computed invalidation
      if (computeCtx) {
        outboundCall.whenInvalidated.promise.then(() => computeCtx.computed.invalidate());
      }
      return value;
    };
    const cf = new ComputeFunction(methodDef.name, rpcImpl);
    const syntheticInstance = {};
    return (...args: unknown[]) => {
      const last = args[args.length - 1];
      const sliced = args.slice(0, methodDef.argCount);
      const invokeArgs = last instanceof AsyncContext ? [...sliced, last] : sliced;
      return cf.invoke(syntheticInstance, invokeArgs).then(c => c.value);
    };
  }
}

function extractDispatchContext(args: unknown[]): RpcDispatchContext | undefined {
  const last = args[args.length - 1];
  if (last !== null && typeof last === "object" && "__rpcDispatch" in last) {
    return last as RpcDispatchContext;
  }
  return undefined;
}
