using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public class RpcNonRoutingInterceptor : RpcInterceptorBase
{
    public readonly object? LocalTarget;
    public readonly bool AssumeConnected;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcNonRoutingInterceptor(
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
        return invocation => {
            Task<TUnwrapped> resultTask;
            var context = invocation.Context as RpcOutboundContext ?? RpcOutboundContext.Current ?? new();
            if (context.Suppressor is { } suppressor) {
                resultTask = (Task<TUnwrapped>)suppressor.Invoke(rpcMethodDef, invocation);
                return rpcMethodDef.WrapAsyncInvokerResultOfAsyncMethod(resultTask);
            }

            var call = (RpcOutboundCall<TUnwrapped>?)context.PrepareCall(rpcMethodDef, invocation.Arguments);
            if (call == null)
                throw RpcRerouteException.MustRerouteToLocal();

            resultTask = call.Invoke(AssumeConnected);
            return rpcMethodDef.WrapAsyncInvokerResultOfAsyncMethod(resultTask);
        };
    }
}
