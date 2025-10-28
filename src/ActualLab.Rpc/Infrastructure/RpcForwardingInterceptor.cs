using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public class RpcForwardingInterceptor : RpcInterceptor
{
    public readonly object? LocalTarget;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcForwardingInterceptor(
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
            using var scope = RpcOutboundContext.UseOrActivateNew();
            var context = scope.Context;
            RpcCallOptions.Use(context, out bool allowRerouting);
            var call = context.PrepareCall(rpcMethodDef, invocation.Arguments);
            if (allowRerouting)
                context.Peer?.ThrowIfRerouted(); // A higher level interceptor should handle it

            Task resultTask;
            if (call is null) { // Local call
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
}
