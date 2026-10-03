using System.Diagnostics;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Middlewares;

namespace ActualLab.Fusion.Rpc;

/// <summary>
/// An RPC middleware that wraps inbound compute method calls in a <see cref="ComputeContext"/>
/// to capture the resulting <see cref="Computed"/> instance.
/// </summary>
public class RpcInboundComputeCallHandler : IRpcMiddleware
{
    public static Func<RpcMethodDef, bool> DefaultFilter { get; set; } = _ => true;
    // What an inbound invalidation does when it lands on a host that doesn't own the value -
    // i.e. when the shard moved between the routing and the arrival. Doing nothing is right by
    // default: the new owner has no value to invalidate, and the old one dropped its own when it
    // lost the shard. Set it to true to surface the misroute as a reroute instead.
    public static bool DefaultMustRerouteInvalidations { get; set; }

    public double Priority { get; init; } = RpcInboundMiddlewarePriority.Final;
    public Func<RpcMethodDef, bool> Filter { get; init; } = DefaultFilter;
    public bool MustRerouteInvalidations { get; init; } = DefaultMustRerouteInvalidations;

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
            if (call is IRpcInboundInvalidateCall) {
                call.Context.Peer.Ref.RequireBackend();
                if (!methodDef.IsLocalCall(call.Arguments!))
                    return MustRerouteInvalidations
                        ? throw RpcRerouteException.MustRerouteInbound()
                        : default(T)!;

                var source = new InvalidationSource($"Routed invalidation from {call.Context.Peer.Ref}");
                using var _1 = Invalidation.Begin(source);
                _ = next.Invoke(call); // Invalidation calls complete synchronously
                return default!;
            }

            var typedCall = (RpcInboundComputeCall<T>)call;

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
