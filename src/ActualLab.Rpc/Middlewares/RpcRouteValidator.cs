using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Middlewares;

/// <summary>
/// An <see cref="IRpcMiddleware"/> that validates inbound call routing for distributed and client-only services.
/// </summary>
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

        var serviceMode = methodDef.Service.Mode;
        if (serviceMode is RpcServiceMode.Client)
            return _ => throw Errors.PureClientCannotProcessInboundCalls(methodDef.Service.Name);

        if (serviceMode == RpcServiceMode.Distributed)
            return call => {
                // Distributed services ensure the call is routed to a local peer
                // and throw RpcRerouteException if it's not (see RouteInboundCall logic).
                var peer = call.MethodDef.RouteInboundCall(call.Arguments!);
                // RpcRoutingMode.Inbound means the rerouting logic in RpcInterceptor kicks in.
                using (new RpcOutboundCallSetup(peer, RpcRoutingMode.Inbound).Activate())
#pragma warning disable MA0100, RCS1229
                    return next.Invoke(call); // No "await" is intended here!
#pragma warning restore MA0100, RCS1229
            };

        return next;
    }
}
