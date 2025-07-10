using ActualLab.Fusion.Internal;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Interception;

public sealed class DisableRpcWhileInvalidatingInterceptor(
    RpcInterceptorOptions settings,
    IServiceProvider services,
    RpcServiceDef serviceDef,
    RpcRoutingInterceptor nextInterceptor)
    : RpcRoutingInterceptor(settings, services, serviceDef, nextInterceptor.LocalTarget, nextInterceptor.AssumeConnected)
{
    public readonly RpcInterceptor NextInterceptor = nextInterceptor;

    protected override Func<Invocation, object?>? CreateUntypedHandler(Invocation initialInvocation, MethodDef methodDef)
    {
        var nextHandler = NextInterceptor.GetHandler(initialInvocation);
        if (nextHandler is null)
            return null;

        var rpcMethodDef = (RpcMethodDef)methodDef;
        var localCallAsyncInvoker = methodDef.SelectAsyncInvokerUntyped(initialInvocation.Proxy, LocalTarget);
        return invocation => {
            if (!Invalidation.IsActive)
                return nextHandler.Invoke(invocation);

            if (!rpcMethodDef.Service.HasServer)
                throw Errors.RpcDisabled(); // Invalidation is active, but the service is not available on the server.

            // Switch to the local call for invalidation - all hosts should be invalidated.
            if (localCallAsyncInvoker == null)
                throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

            var resultTask = localCallAsyncInvoker.Invoke(invocation);
            return rpcMethodDef.UniversalAsyncResultWrapper.Invoke(resultTask);
        };
    }
}
