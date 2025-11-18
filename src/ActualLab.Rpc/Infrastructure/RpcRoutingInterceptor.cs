using ActualLab.Interception;

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

            if (context.AllowRerouting && peer.Ref.RouteState is not null)
                resultTask = InvokeWithRerouting(rpcMethodDef, context, call, localCallAsyncInvoker, invocation);
            else if (call is null) { // Local call
                if (localCallAsyncInvoker is null)
                    throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

                resultTask = localCallAsyncInvoker.Invoke(invocation);
            }
            else
                resultTask = call.Invoke();
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
            try {
                var peer = context.Peer!;
                var routeState = peer.Ref.RouteState;
                routeState.ThrowIfChanged();

                var routeChangedToken = routeState?.ChangedToken ?? default;
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, routeChangedToken);
                if (methodDef.CancellationTokenIndex >= 0)
                    invocation.Arguments.SetCancellationToken(methodDef.CancellationTokenIndex, linkedCts.Token);

                try {
                    if (call is not null)
                        return await call.Invoke().ConfigureAwait(false);

                    if (localCallAsyncInvoker is null)
                        throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it


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
                if (call is null && localCallAsyncInvoker is null)
                    throw; // A higher level interceptor should handle it

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
