using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public class RpcRoutingInterceptor : RpcInterceptor
{
    public readonly object? LocalTarget;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcRoutingInterceptor(
        RpcInterceptorOptions settings, IServiceProvider services,
        RpcServiceDef serviceDef,
        object? localTarget
        ) : base(settings, services, serviceDef)
        => LocalTarget = localTarget;

    protected override Func<Invocation, object?>? CreateUntypedHandler(Invocation initialInvocation, MethodDef methodDef)
    {
        var rpcMethodDef = (RpcMethodDef)methodDef;
        var localCallAsyncInvoker = methodDef.SelectAsyncInvokerUntyped(initialInvocation.Proxy, LocalTarget);
        return invocation => {
            Task resultTask;
            using var scope = RpcOutboundContext.UseOrActivateNew();
            var context = scope.Context;
            RpcCallOptions.Use(context, out var allowRerouting);
            var call = context.PrepareCall(rpcMethodDef, invocation.Arguments);
            var peer = context.Peer!;

            if (allowRerouting && peer.Ref.CanBeRerouted)
                resultTask = InvokeWithRerouting(rpcMethodDef, context, call, localCallAsyncInvoker, invocation);
            else if (call is null) { // Local call
                if (localCallAsyncInvoker is null)
                    throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

                using var _ = RpcOutboundContext.Deactivate();
                resultTask = localCallAsyncInvoker.Invoke(invocation);
            }
            else
                resultTask = call.Invoke();
            return rpcMethodDef.UniversalAsyncResultWrapper.Invoke(resultTask);
        };
    }

    protected async Task<object?> InvokeWithRerouting(
        RpcMethodDef methodDef,
        RpcOutboundContext context,
        RpcOutboundCall? call,
        Func<Invocation, Task>? localCallAsyncInvoker,
        Invocation invocation)
    {
        var cancellationToken = context.CallCancelToken;
        while (true) {
            try {
                if (call is not null)
                    return await call.Invoke().ConfigureAwait(false);

                if (localCallAsyncInvoker is null)
                    throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

                Task untypedResultTask;
                using (RpcOutboundContext.Deactivate()) // No RPC expected -> hide RpcOutboundContext
                    untypedResultTask = localCallAsyncInvoker.Invoke(invocation);
                return await methodDef.TaskToUntypedValueTaskConverter
                    .Invoke(untypedResultTask)
                    .ConfigureAwait(false);
            }
            catch (RpcRerouteException e) {
                if (call is null && localCallAsyncInvoker is null)
                    throw; // A higher level interceptor should handle it

                Log.LogWarning(e, "Rerouting: {Invocation}", invocation);
                await Hub.RerouteDelayer.Invoke(cancellationToken).ConfigureAwait(false);
                call = context.PrepareReroutedCall();
            }
        }
    }
}
