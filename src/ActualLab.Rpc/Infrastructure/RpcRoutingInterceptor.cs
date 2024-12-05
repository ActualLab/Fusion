using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public class RpcRoutingInterceptor : RpcInterceptorBase
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

    protected override Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>
        (Invocation initialInvocation, MethodDef methodDef)
    {
        var rpcMethodDef = (RpcMethodDef)methodDef;
        var localCallAsyncInvoker = methodDef.SelectAsyncInvoker<TUnwrapped>(initialInvocation.Proxy, LocalTarget);
        return invocation => {
            Task<TUnwrapped> resultTask;
            var context = invocation.Context as RpcOutboundContext ?? RpcOutboundContext.Current ?? new();
            if (context.Suppressor is { } suppressor) {
                resultTask = (Task<TUnwrapped>)suppressor.Invoke(rpcMethodDef, invocation);
                return rpcMethodDef.WrapAsyncInvokerResultOfAsyncMethod(resultTask);
            }

            var call = (RpcOutboundCall<TUnwrapped>?)context.PrepareCall(rpcMethodDef, invocation.Arguments);
            var peer = context.Peer!;
            if (peer.Ref.CanBeRerouted)
                resultTask = InvokeWithRerouting(invocation, context, call, localCallAsyncInvoker);
            else if (call == null) { // Local call
                if (localCallAsyncInvoker == null)
                    throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

                resultTask = localCallAsyncInvoker.Invoke(invocation);
            }
            else
                resultTask = call.Invoke(AssumeConnected);

            return rpcMethodDef.WrapAsyncInvokerResultOfAsyncMethod(resultTask);
        };
    }

    private async Task<T> InvokeWithRerouting<T>(
        Invocation invocation,
        RpcOutboundContext context,
        RpcOutboundCall<T>? call,
        Func<Invocation, Task<T>>? localCallAsyncInvoker)
    {
        var cancellationToken = context.CallCancelToken;
        while (true) {
            if (call == null)
                return localCallAsyncInvoker != null
                    ? await localCallAsyncInvoker.Invoke(invocation).ConfigureAwait(false)
                    : throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

            try {
                return await call.Invoke(AssumeConnected).ConfigureAwait(false);
            }
            catch (RpcRerouteException e) {
                Log.LogWarning(e, "Rerouting: {Invocation}", invocation);
                await Hub.RerouteDelayer.Invoke(cancellationToken).ConfigureAwait(false);
                call = (RpcOutboundCall<T>?)context.PrepareReroutedCall();
            }
            catch (Exception e) {
                Log.LogWarning(e, "[Debug] Exception!");
                throw;
            }
        }
    }
}
