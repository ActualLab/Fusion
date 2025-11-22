using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcInterceptor : Interceptor
{
    public new sealed record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    public readonly RpcHub Hub;
    public readonly RpcServiceDef ServiceDef;
    public readonly object? LocalTarget;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcInterceptor(
        Options settings,
        RpcHub hub,
        RpcServiceDef serviceDef,
        object? localTarget
        ) : base(settings, hub.Services)
    {
        Hub = hub;
        ServiceDef = serviceDef;
        UsesUntypedHandlers = true;
        LocalTarget = localTarget;
    }

    protected override Func<Invocation, object?>? CreateUntypedHandler(Invocation initialInvocation, MethodDef methodDef)
    {
        var rpcMethodDef = (RpcMethodDef)methodDef;
        var localCallAsyncInvoker = methodDef.SelectAsyncInvokerUntyped(initialInvocation.Proxy, LocalTarget);
        return invocation => {
            Task resultTask;
            var context = RpcOutboundCallSetup.ProduceContext();
            var call = context.PrepareCall(rpcMethodDef, invocation.Arguments);
            var peer = context.Peer!;
            var routingMode = context.RoutingMode;

            if (routingMode is RpcRoutingMode.Outbound) {
                if (peer.Ref.RouteState is null) {
                    // There is no RoutingState, and thus no rerouting and no shard routing
                    if (call is not null) {
                        // Direct outbound RPC call
                        resultTask = call.Invoke();
                    }
                    else {
                        // call is null
                        if (localCallAsyncInvoker is null)
                            throw Errors.CannotExecuteInterfaceCallLocally();

                        // Direct local call
                        resultTask = localCallAsyncInvoker.Invoke(invocation);
                    }
                }
                else // There is a RoutingState, and thus rerouting / shard routing is possible
                    resultTask = InvokeWithRerouting(rpcMethodDef, context, call, localCallAsyncInvoker, invocation);
            }
            else { // RoutingMode is Inbound or None
                // The call is prerouted via RpcOutboundCallSetup, and there are two cases:
                // - It can be pre-routed to a non-local peer. It's typically done for NoWait and System calls, and all we need is to send it right away.
                // - And it can be pre-routed to a local peer.
                //   It may happen when an RPC service gets an inbound RPC call, and typically the mode is:
                //   - Inbound (= route, throw reroute exception if non-local) for Distributed services, and
                //   - None (= always route to the local peer) for any other service type.
                if (call is not null) {
                    if (routingMode is RpcRoutingMode.Inbound) // This should never happen
                        throw ActualLab.Internal.Errors.InternalError(
                            $"{nameof(RpcRoutingMode)} is {nameof(RpcRoutingMode.Inbound)}, "
                            + $"but the call is pre-routed to a remote {nameof(RpcPeer)}.");

                    // RoutingMode is None
                    // Direct outbound RPC call
                    resultTask = call.Invoke();
                }
                else {
                    // call is null
                    if (routingMode is RpcRoutingMode.Prerouted) {
                        if (localCallAsyncInvoker is null)
                            throw Errors.CannotExecuteInterfaceCallLocally();

                        // Direct local call
                        resultTask = localCallAsyncInvoker.Invoke(invocation);
                    }
                    else {
                        // Local call, but with rerouting / shard routing
                        resultTask = InvokeWithRerouting(rpcMethodDef, context, call, localCallAsyncInvoker,
                            invocation);
                    }
                }
            }

            return rpcMethodDef.UniversalAsyncResultConverter.Invoke(resultTask);
        };
    }

    private async Task<object?> InvokeWithRerouting(
        RpcMethodDef methodDef,
        RpcOutboundContext context,
        RpcOutboundCall? call,
        Func<Invocation, Task>? localCallAsyncInvoker,
        Invocation invocation)
    {
        var mustRerouteUnlessLocal = context.RoutingMode is RpcRoutingMode.Inbound;
        var cancellationToken = context.CancellationToken;
        var rerouteCount = 0;
        while (true) {
            var peer = context.Peer!;
            var routeState = peer.Ref.RouteState; // May become null after rerouting
            var shardRouteState = routeState.AsShardRouteState(methodDef);
            CancellationToken routeChangedToken = default;
            try {
                routeState.RerouteIfChanged();
                CancellationTokenSource? linkedCts = null;
                try {
                    if (call is not null) {
                        if (mustRerouteUnlessLocal) {
                            // If we're here, the call was prerouted to a local peer,
                            // but was re-routed to a non-local peer later.
                            throw RpcRerouteException.MustRerouteInbound();
                        }

                        return await call.Invoke().ConfigureAwait(false);
                    }

                    if (localCallAsyncInvoker is null)
                        throw Errors.CannotExecuteInterfaceCallLocally();

                    if (shardRouteState is not null)
                        routeChangedToken = await shardRouteState.ShardLockAwaiter
                            .Invoke(cancellationToken)
                            .ConfigureAwait(false);
                    else if (routeState is not null)
                        routeChangedToken = routeState.ChangedToken;

                    if (routeChangedToken.CanBeCanceled) {
                        linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, routeChangedToken);
                        if (methodDef.CancellationTokenIndex >= 0)
                            invocation.Arguments.SetCancellationToken(methodDef.CancellationTokenIndex, linkedCts.Token);
                    }

                    var untypedResultTask = localCallAsyncInvoker.Invoke(invocation);
                    return await methodDef.TaskToObjectValueTaskConverter
                        .Invoke(untypedResultTask)
                        .ConfigureAwait(false);
                }
                catch (Exception e) when (e.IsCancellationOf(routeChangedToken) && !cancellationToken.IsCancellationRequested) {
                    throw new RpcRerouteException(routeChangedToken);
                }
                finally {
                    linkedCts.DisposeSilently();
                }
            }
            catch (RpcRerouteException e) {
                if (methodDef.CancellationTokenIndex >= 0)
                    invocation.Arguments.SetCancellationToken(methodDef.CancellationTokenIndex, cancellationToken);
                if (shardRouteState is not null && !shardRouteState.IsChanged()) {
                    Log.LogWarning(e, "Re-acquiring shard ownership: {Invocation}", invocation);
                    continue;
                }

                ++rerouteCount;
                Log.LogWarning(e, "Rerouting #{RerouteCount}: {Invocation}", rerouteCount, invocation);
                await Hub.OutboundCallOptions
                    .GetReroutingDelay(rerouteCount, cancellationToken)
                    .ConfigureAwait(false);
                call = context.PrepareReroutedCall();
            }
        }
    }

    public override MethodDef? GetMethodDef(MethodInfo method, Type proxyType)
        => ServiceDef.FindMethod(method);

    protected override MethodDef? CreateMethodDef(MethodInfo method,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyType)
        => ServiceDef.FindMethod(method);
}
