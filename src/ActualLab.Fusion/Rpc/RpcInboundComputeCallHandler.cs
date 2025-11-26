using System.Diagnostics;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Middlewares;

namespace ActualLab.Fusion.Rpc;

public class RpcInboundComputeCallHandler : IRpcMiddleware
{
    public static Func<RpcMethodDef, bool> DefaultFilter { get; set; } = _ => true;

    public double Priority { get; init; } = RpcInboundMiddlewarePriority.Final;
    public Func<RpcMethodDef, bool> Filter { get; init; } = DefaultFilter;

    public Func<RpcInboundCall, Task<T>> Create<T>(RpcMiddlewareContext<T> context, Func<RpcInboundCall, Task<T>> next)
    {
        var methodDef = context.MethodDef;
        if (methodDef is not RpcComputeMethodDef)
            return next;

        // The line below suppresses the RpcRouteValidator middleware.
        // RemoteComputeMethodFunction.ProduceComputedImpl handles "reroute unless local" logic.
        // Search for ".RouteOutboundCall" there to see how it works.
        context.RemainingMiddlewares.RemoveAll(x => x is RpcRouteValidator);

        // This logic is a part of RpcRouteValidator middleware we just suppressed, so we keep it here
        if (methodDef.Service.Mode is RpcServiceMode.Client)
            return _ => throw Errors.PureClientCannotProcessInboundCalls(methodDef.Service.Name);

        return async call => {
            var typedCall = (RpcInboundComputeCall<T>)call;
            Debug.Assert(ComputeContext.Current == ComputeContext.None);

            // We can't use RpcOutgoingCallSettings for the same purpose here, because ProduceComputedImpl
            // is typically called from a post-async-lock block, so the original RpcOutgoingCallSettings.Peer
            // won't be available at this point.
            var computeContext = new ComputeContext(CallOptions.Capture | CallOptions.InboundRpc);
            ComputeContext.Current = computeContext;
            try {
                return await next.Invoke(call).ConfigureAwait(false);
            }
            finally {
                ComputeContext.Current = null!;
                var computed = computeContext.TryGetCaptured<T>();
                if (computed is not null) {
                    lock (typedCall.Lock)
                        typedCall.Computed ??= computed;
                }
            }
        };
    }
}
