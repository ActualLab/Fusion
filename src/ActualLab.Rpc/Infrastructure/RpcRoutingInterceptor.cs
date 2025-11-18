using ActualLab.Interception;
using ActualLab.Reflection.Internal;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcRoutingInterceptor : RpcServiceInterceptor
{
    public new sealed record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    public readonly object? LocalTarget;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcRoutingInterceptor(
        Options settings,
        RpcHub hub,
        RpcServiceDef serviceDef,
        object? localTarget
        ) : base(settings, hub, serviceDef)
        => LocalTarget = localTarget;

    protected override Func<Invocation, object?>? CreateUntypedHandler(Invocation initialInvocation, MethodDef methodDef)
    {
        var rpcMethodDef = (RpcMethodDef)methodDef;
        var localCallAsyncInvoker = methodDef.SelectAsyncInvokerUntyped(initialInvocation.Proxy, LocalTarget);
        return invocation => {
            Task resultTask;
            var context = RpcOutboundCallSetup.ProduceContext();
            var call = context.PrepareCall(rpcMethodDef, invocation.Arguments);
            var peer = context.Peer!;

            if (context.IsPrerouted) {
                // The call is prerouted, which implies it has to be an RPC call
                if (call is null)
                    throw RpcRerouteException.MustReroutePrerouted();

                resultTask = call.Invoke();
            }
            else if (peer.Ref.RouteState is null) {
                // There is no RoutingState, and thus no rerouting and no shard routing
                if (call is null) {
                    if (localCallAsyncInvoker is null)
                        throw Errors.CannotExecuteInterfaceCallLocally();

                    resultTask = localCallAsyncInvoker.Invoke(invocation);
                }
                else
                    resultTask = call.Invoke();
            }
            else // There is a RoutingState, and thus rerouting / shard routing is possible
                resultTask = InvokeWithRerouting(rpcMethodDef, context, call, localCallAsyncInvoker, invocation);

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
        var cancellationToken = context.CancellationToken;
        var rerouteCount = 0;
        while (true) {
            var peer = context.Peer!;
            var routeState = peer.Ref.RouteState;
            var shardRouteState = methodDef.OutboundCallShardRoutingMode is RpcShardRoutingMode.Default
                ? routeState as RpcShardRouteState
                : null;
            CancellationToken routeChangedToken = default;
            try {
                routeState.ThrowIfChanged();
                CancellationTokenSource? linkedCts = null;
                try {
                    if (call is not null) // Outbound RPC call
                        return await call.Invoke().ConfigureAwait(false);

                    if (localCallAsyncInvoker is null)
                        throw Errors.CannotExecuteInterfaceCallLocally();

                    if (shardRouteState is not null)
                        routeChangedToken = await shardRouteState.WhenShardOwned(cancellationToken).ConfigureAwait(false);
                    else if (routeState is not null)
                        routeChangedToken = routeState.ChangedToken;

                    linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, routeChangedToken);
                    if (methodDef.CancellationTokenIndex >= 0)
                        invocation.Arguments.SetCancellationToken(methodDef.CancellationTokenIndex, linkedCts.Token);

                    var untypedResultTask = localCallAsyncInvoker.Invoke(invocation);
                    return await methodDef.TaskToObjectValueTaskConverter
                        .Invoke(untypedResultTask)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && routeChangedToken.IsCancellationRequested) {
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
}
