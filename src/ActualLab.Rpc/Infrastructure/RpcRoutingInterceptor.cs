using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public class RpcRoutingInterceptor : RpcInterceptor
{
    public readonly object? LocalTarget;
    public readonly bool AssumeConnected;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcRoutingInterceptor(
        RpcInterceptorOptions settings, IServiceProvider services,
        RpcServiceDef serviceDef,
        object? localTarget,
        bool assumeConnected = false
    ) : base(settings, services, serviceDef)
    {
        LocalTarget = localTarget;
        AssumeConnected = assumeConnected;
    }

    protected override Func<Invocation, object?>? CreateUntypedHandler(Invocation initialInvocation, MethodDef methodDef)
    {
        var rpcMethodDef = (RpcMethodDef)methodDef;
        var localCallAsyncInvoker = methodDef.SelectAsyncInvokerUntyped(initialInvocation.Proxy, LocalTarget);
        return invocation => {
            Task resultTask;
            using var scope = RpcOutboundContext.UseOrActivateNew();
            var context = scope.Context;
            var overridePeer = RpcCallRouteOverride.ApplyAndReset(context);
            var call = context.PrepareCall(rpcMethodDef, invocation.Arguments);
            var peer = context.Peer!;
            if (overridePeer is null && peer.Ref.CanBeRerouted)
                resultTask = InvokeWithRerouting(rpcMethodDef, context, call, localCallAsyncInvoker, invocation);
            else if (call is null) { // Local call
                if (localCallAsyncInvoker is null)
                    throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

                using var _ = RpcOutboundContext.Deactivate();
                resultTask = localCallAsyncInvoker.Invoke(invocation);
            }
            else
                resultTask = call.Invoke(AssumeConnected);
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
        for (var tryIndex = 0;; tryIndex++) {
            if (call is null) {
                if (localCallAsyncInvoker is null)
                    throw RpcRerouteException.MustRerouteToLocal();

                Task resultTask;
                using (RpcOutboundContext.Deactivate())
                    resultTask = localCallAsyncInvoker.Invoke(invocation);
                return await methodDef.TaskToUntypedValueTaskConverter.Invoke(resultTask).ConfigureAwait(false);
            }

            try {
                Task<object?> resultTask;
                using (tryIndex != 0 ? context.Activate() : default) // .Activate() is needed only after "await"
                    resultTask = call.Invoke(AssumeConnected);
                return await resultTask.ConfigureAwait(false);
            }
            catch (RpcRerouteException e) {
                Log.LogWarning(e, "Rerouting: {Invocation}", invocation);
                await Hub.RerouteDelayer.Invoke(cancellationToken).ConfigureAwait(false);
                call = context.PrepareReroutedCall();
            }
        }
    }
}
