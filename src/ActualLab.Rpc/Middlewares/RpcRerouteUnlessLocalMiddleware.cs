using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Middlewares;

public sealed record RpcRerouteUnlessLocalMiddleware : IRpcMiddleware
{
    public static Func<RpcMethodDef, bool> DefaultFilter { get; set; } = _ => true;

    public double Priority { get; init; } = RpcInboundMiddlewarePriority.Final + 1; // Pre-final
    public Func<RpcMethodDef, bool> Filter { get; init; } = DefaultFilter;

    public Func<RpcInboundCall, Task<T>> Create<T>(RpcMiddlewareContext<T> context, Func<RpcInboundCall, Task<T>> next)
    {
        var methodDef = context.MethodDef;
        if (methodDef.Service.Mode is not RpcServiceMode.Distributed || !Filter.Invoke(methodDef))
            return next;

        return call => {
            var peer = call.MethodDef.RouteOutboundCall(call.Arguments!);
            if (peer.ConnectionKind is not RpcPeerConnectionKind.Local)
                throw RpcRerouteException.MustRerouteInbound();

#pragma warning disable MA0100
            using (new RpcOutboundCallSetup(peer, RpcRoutingMode.LocalOnly).Activate())
                return next.Invoke(call);
#pragma warning restore MA0100
        };
    }
}
