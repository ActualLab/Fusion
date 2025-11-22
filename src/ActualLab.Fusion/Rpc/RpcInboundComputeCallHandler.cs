using System.Diagnostics;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
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

        // RemoteComputeMethodFunction.ProduceComputedImpl handles "reroute unless local" logic.
        // Search for ".RouteOutboundCall" there to see how it works.
        context.RemainingMiddlewares.RemoveAll(x => x is RpcRerouteUnlessLocalMiddleware);

        return async call => {
            var typedCall = (RpcInboundComputeCall<T>)call;
            Debug.Assert(ComputeContext.Current == ComputeContext.None);
            var computeContext = new ComputeContext(CallOptions.Capture | CallOptions.RerouteUnlessLocal);
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
