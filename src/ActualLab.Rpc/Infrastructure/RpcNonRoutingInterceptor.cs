using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public class RpcNonRoutingInterceptor : RpcInterceptor
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

    protected override Func<Invocation, object?>? CreateUntypedHandler(Invocation initialInvocation, MethodDef methodDef)
    {
        var rpcMethodDef = (RpcMethodDef)methodDef;
        return invocation => {
            var context = invocation.Context as RpcOutboundContext ?? RpcOutboundContext.Current ?? new();
            var call = context.PrepareCall(rpcMethodDef, invocation.Arguments);
            if (call == null)
                throw RpcRerouteException.MustRerouteToLocal();

            var resultTask = call.Invoke(AssumeConnected);
            return rpcMethodDef.UniversalAsyncResultWrapper.Invoke(resultTask);
        };
    }
}
