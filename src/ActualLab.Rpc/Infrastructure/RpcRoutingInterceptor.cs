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

            if (context.AllowRerouting && peer.Ref.CanBeRerouted)
                resultTask = InvokeWithRerouting(rpcMethodDef, context, call, localCallAsyncInvoker, invocation);
            else if (call is null) { // Local call
                if (localCallAsyncInvoker is null)
                    throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

                // Honor RerouteToken for local calls
                resultTask = peer.Ref.CanBeRerouted
                    ? InvokeLocalWithRerouting(localCallAsyncInvoker, invocation, peer.Ref)
                    : localCallAsyncInvoker.Invoke(invocation);
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
                if (call is not null)
                    return await call.Invoke().ConfigureAwait(false);

                if (localCallAsyncInvoker is null)
                    throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

                var untypedResultTask = localCallAsyncInvoker.Invoke(invocation);
                return await methodDef.TaskToObjectValueTaskConverter
                    .Invoke(untypedResultTask)
                    .ConfigureAwait(false);
            }
            catch (RpcRerouteException e) {
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

    private static async Task InvokeLocalWithRerouting(
        Func<Invocation, Task> localCallAsyncInvoker,
        Invocation invocation,
        RpcPeerRef peerRef)
    {
        var invokeTask = localCallAsyncInvoker.Invoke(invocation);
        var whenReroutedTask = peerRef.WhenRerouted();
        var completedTask = await Task.WhenAny(invokeTask, whenReroutedTask).ConfigureAwait(false);
        if (ReferenceEquals(completedTask, whenReroutedTask))
            throw RpcRerouteException.MustReroute();

        await invokeTask.ConfigureAwait(false);
    }
}
