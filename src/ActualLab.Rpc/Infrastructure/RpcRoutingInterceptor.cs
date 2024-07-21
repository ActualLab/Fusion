using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#if !NET5_0
[RequiresUnreferencedCode(UnreferencedCode.Rpc)]
#endif
public class RpcRoutingInterceptor : RpcInterceptorBase
{
    public new record Options : RpcInterceptorBase.Options
    {
        public static Options Default { get; set; } = new();
    }

    public readonly object? LocalTarget;
    public readonly bool AssumeConnected;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcRoutingInterceptor(
        Options settings, IServiceProvider services,
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
            var context = invocation.Context as RpcOutboundContext ?? new();
            var call = (RpcOutboundCall<TUnwrapped>?)context.PrepareCall(rpcMethodDef, invocation.Arguments);
            var peer = context.Peer!;
            Task<TUnwrapped> resultTask;
            if (peer.Ref.CanBeRerouted)
                resultTask = InvokeWithRerouting(invocation, context, call, localCallAsyncInvoker);
            else if (call == null) { // Local call
                if (localCallAsyncInvoker == null)
                    throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

                resultTask = localCallAsyncInvoker.Invoke(invocation);
            }
            else
                resultTask = call.Invoke(AssumeConnected);

            return rpcMethodDef.WrapAsyncInvokerResultAssumeAsync(resultTask);
        };
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    private async Task<T> InvokeWithRerouting<T>(
        Invocation invocation,
        RpcOutboundContext context,
        RpcOutboundCall<T>? call,
        Func<Invocation, Task<T>>? localCallAsyncInvoker)
    {
        var cancellationToken = context.CancellationToken;
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
