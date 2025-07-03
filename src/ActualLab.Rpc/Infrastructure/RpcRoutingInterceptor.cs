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
            var context = invocation.Context as RpcOutboundContext ?? RpcOutboundContext.Current ?? new();
            var call = context.PrepareCall(rpcMethodDef, invocation.Arguments);
            var peer = context.Peer!;
            if (peer.Ref.CanBeRerouted)
                resultTask = InvokeWithRerouting(invocation, rpcMethodDef, context, call, localCallAsyncInvoker);
            else if (call == null) { // Local call
                if (localCallAsyncInvoker == null)
                    throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

                resultTask = localCallAsyncInvoker.Invoke(invocation);
            }
            else
                resultTask = call.Invoke(AssumeConnected);
            return rpcMethodDef.UniversalAsyncResultWrapper.Invoke(resultTask);
        };
    }

    protected async Task<object?> InvokeWithRerouting(
        Invocation invocation,
        RpcMethodDef methodDef,
        RpcOutboundContext context,
        RpcOutboundCall? call,
        Func<Invocation, Task>? localCallAsyncInvoker)
    {
        var cancellationToken = context.CallCancelToken;
        while (true) {
            if (call == null) {
                if (localCallAsyncInvoker == null)
                    throw RpcRerouteException.MustRerouteToLocal();

                var resultTask = localCallAsyncInvoker.Invoke(invocation);
                return await methodDef.TaskToUntypedValueTaskConverter.Invoke(resultTask).ConfigureAwait(false);
            }

            try {
                return await call.Invoke(AssumeConnected).ConfigureAwait(false);
            }
            catch (RpcRerouteException e) {
                Log.LogWarning(e, "Rerouting: {Invocation}", invocation);
                await Hub.RerouteDelayer.Invoke(cancellationToken).ConfigureAwait(false);
                call = context.PrepareReroutedCall();
            }
            catch (Exception e) {
                Log.LogWarning(e, "[Debug] Exception!");
                throw;
            }
        }
    }
}
