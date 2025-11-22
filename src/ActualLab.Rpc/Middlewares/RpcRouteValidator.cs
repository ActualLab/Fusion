using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Middlewares;

public sealed record RpcRouteValidator : IRpcMiddleware
{
    public static Func<RpcMethodDef, bool> DefaultFilter { get; set; } = _ => true;

    public double Priority { get; init; } = RpcInboundMiddlewarePriority.Final + 1; // Pre-final
    public Func<RpcMethodDef, bool> Filter { get; init; } = DefaultFilter;

    public Func<RpcInboundCall, Task<T>> Create<T>(RpcMiddlewareContext<T> context, Func<RpcInboundCall, Task<T>> next)
    {
        var methodDef = context.MethodDef;
        if (!Filter.Invoke(methodDef))
            return next;

#pragma warning disable MA0100, RCS1229
        var hub = methodDef.Hub;
        if (methodDef.Service.Mode is not RpcServiceMode.Distributed)
            return call => {
                // Regular services always process the call locally
                using (new RpcOutboundCallSetup(hub.LocalPeer, RpcRoutingMode.Prerouted).Activate())
                    return next.Invoke(call); // No "await" is intended here!
            };

        return call => {
            // Distributed services validate the call is routed to a local peer
            // and throw RpcRerouteException if it's not.
            // RpcRoutingMode.Inbound means the rerouting and ShardLockAwaiter logic
            // in RpcInterceptor must kick in.
            var peer = call.MethodDef.RouteInboundCall(call.Arguments!);
            using (new RpcOutboundCallSetup(peer, RpcRoutingMode.Inbound).Activate())
                return next.Invoke(call); // No "await" is intended here!
        };
#pragma warning restore MA0100, RCS1229
    }
}
