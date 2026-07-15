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
    RpcSystemCallSender,
    defineRpcService,
    wireMethodName,
    type RpcConnection,
    type RpcSerializationFormat,
    type WebSocketLike,
    type RpcServiceDef,
    type RpcMethodDef,
    type RpcMethodDefInput,
    type RpcDispatchContext,
    type RpcPeer,
    type RpcMessage,
    type RpcCallOptions,
    getMethodsMeta,
} from '@actuallab/rpc';
import {
    ComputeFunction,
    ComputeContext,
    type ComputeFunctionImpl,
} from '@actuallab/fusion';
import { AsyncContext } from '@actuallab/core';
import { RpcOutboundComputeCall } from './rpc-outbound-compute-call.js';

/** Call type ID for Fusion compute calls — matches .NET's RpcComputeCallType.Id. */
export const FUSION_CALL_TYPE_ID = 1;

/** Wire method name for invalidation system call. */
const FUSION_INVALIDATE_METHOD = '$sys-c.Invalidate:0';

/** Handles Fusion-specific system calls ($sys-c.Invalidate), delegating the rest to base. */
class FusionSystemCallHandler extends RpcSystemCallHandler {
    override handle(message: RpcMessage, args: unknown[], peer: RpcPeer): void {
        const method = message.Method;
        const relatedId = message.RelatedId ?? 0;

        if (method === FUSION_INVALIDATE_METHOD) {
            const call = peer.outboundCalls.remove(relatedId);
            if (call instanceof RpcOutboundComputeCall) {
                call.whenInvalidated.resolve();
            }
            return;
        }

        super.handle(message, args, peer);
    }
}

/** Sends the Fusion $sys-c.Invalidate system call — mirrors .NET's RpcComputeSystemCallSender. */
class FusionSystemCallSender extends RpcSystemCallSender {
    invalidate(
        conn: RpcConnection,
        format: RpcSerializationFormat,
        relatedId: number
    ): void {
        this._send(conn, format, {
            Method: FUSION_INVALIDATE_METHOD,
            RelatedId: relatedId,
        });
    }
}

/** Creates a compute service definition — all methods default to FUSION_CALL_TYPE_ID. */
export function defineComputeService(
    name: string,
    methods: Record<string, RpcMethodDefInput>
): RpcServiceDef {
    const withCallType: Record<string, RpcMethodDefInput> = {};
    for (const [methodName, input] of Object.entries(methods)) {
        withCallType[methodName] = {
            ...input,
            callTypeId: input.callTypeId ?? FUSION_CALL_TYPE_ID,
        };
    }
    return defineRpcService(name, withCallType);
}

/** Central coordinator for Fusion + RPC — manages compute services, invalidation wiring. */
export class FusionHub extends RpcHub {
    declare readonly systemCallSender: FusionSystemCallSender;

    constructor(hubId?: string) {
        super(hubId);
        this.systemCallHandler = new FusionSystemCallHandler();
        // Register Fusion-specific system call for compact format hash resolution
        this.registry.register(FUSION_INVALIDATE_METHOD);
    }

    protected override _createSystemCallSender(): FusionSystemCallSender {
        return new FusionSystemCallSender();
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

    /** Delegate to the base builder, then patch callTypeId for compute methods —
     *  keeps the base's noWait / remoteExecutionMode handling instead of drifting. */
    protected override _buildServiceDef(
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        cls: abstract new (...args: any[]) => any
    ): RpcServiceDef {
        const def = super._buildServiceDef(cls);
        const methodsMeta = getMethodsMeta(cls) ?? {};
        const methods = new Map<string, RpcMethodDef>();
        for (const [mapKey, methodDef] of def.methods) {
            const isCompute =
                (methodsMeta[methodDef.name] as { compute?: boolean } | undefined)
                    ?.compute === true;
            methods.set(
                mapKey,
                isCompute
                    ? { ...methodDef, callTypeId: FUSION_CALL_TYPE_ID }
                    : methodDef
            );
        }

        return { name: def.name, methods };
    }

    protected override _wrapServerMethod(
        methodDef: RpcMethodDef,
        fn: (...args: unknown[]) => unknown,
        impl: object
    ): (...args: unknown[]) => unknown {
        if (methodDef.callTypeId !== FUSION_CALL_TYPE_ID) return fn;

        // Route through ComputeFunction + wire invalidation → $sys-c.Invalidate
        const cf = new ComputeFunction(
            methodDef.name,
            fn as ComputeFunctionImpl
        );
        return async (...args: unknown[]) => {
            const context = extractDispatchContext(args);
            const cleanArgs = context !== undefined ? args.slice(0, -1) : args;

            const computed = await cf.invoke(impl, cleanArgs);

            // IsRegularCall parity (F7): a Regular call to a compute method returns
            // the result immediately, with no invalidation tracking.
            if (context?.callType === FUSION_CALL_TYPE_ID) {
                const { peer, callId } = context;
                // ProcessStage2 parity (F1): whenInvalidated() resolves immediately
                // for an already-invalidated computed, so an invalidation landing
                // during computation still produces the send.
                void computed.whenInvalidated().then(() => {
                    // C# sends the result (ProcessStage1Plus) before the
                    // invalidation (ProcessStage2). Here the dispatch loop sends
                    // $sys.Ok after this wrapper returns, so defer past that turn —
                    // a client drops an Invalidate that precedes its result.
                    setTimeout(() => {
                        const conn = peer.connection;
                        if (conn !== undefined)
                            this.systemCallSender.invalidate(
                                conn,
                                peer.serializationFormat,
                                callId
                            );
                    }, 0);
                });
            }

            return computed.value;
        };
    }

    protected override _createClientMethod(
        peer: RpcPeer,
        methodDef: RpcMethodDef
    ): (...args: unknown[]) => unknown {
        if (methodDef.callTypeId !== FUSION_CALL_TYPE_ID)
            return super._createClientMethod(peer, methodDef);

        const wireName = wireMethodName(methodDef);
        // Local ComputeFunction whose impl makes an RPC call
        const rpcImpl: ComputeFunctionImpl = async function (
            ...args: unknown[]
        ) {
            // Capture ComputeContext synchronously — AsyncContext.current is the child
            // context set by ComputeFunction.invoke() and won't survive the await below.
            const computeCtx = ComputeContext.from(AsyncContext.current);
            const callOptions: RpcCallOptions = {
                callTypeId: FUSION_CALL_TYPE_ID,
                outboundCallFactory: (id, m) =>
                    new RpcOutboundComputeCall(id, m),
            };
            const outboundCall = peer.call(
                wireName,
                args,
                callOptions
            ) as RpcOutboundComputeCall;
            const value = await outboundCall.result;
            // Wire server invalidation → local computed invalidation
            if (computeCtx) {
                void outboundCall.whenInvalidated.then(() =>
                    computeCtx.computed.invalidate()
                );
            }
            return value;
        };
        const cf = new ComputeFunction(methodDef.name, rpcImpl);
        const syntheticInstance = {};
        return (...args: unknown[]) => {
            const last = args[args.length - 1];
            const sliced = args.slice(0, methodDef.argCount);
            const invokeArgs =
                last instanceof AsyncContext ? [...sliced, last] : sliced;
            return cf.invoke(syntheticInstance, invokeArgs).then(c => c.value);
        };
    }
}

function extractDispatchContext(
    args: unknown[]
): RpcDispatchContext | undefined {
    const last = args[args.length - 1];
    if (last !== null && typeof last === 'object' && '__rpcDispatch' in last) {
        return last as RpcDispatchContext;
    }
    return undefined;
}
