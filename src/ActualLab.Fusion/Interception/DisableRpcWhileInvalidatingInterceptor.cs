using ActualLab.Fusion.Internal;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Interception;

public sealed class DisableRpcWhileInvalidatingInterceptor(
    RpcInterceptorOptions settings,
    IServiceProvider services,
    RpcServiceDef serviceDef,
    RpcRoutingInterceptor nextInterceptor
    ) : RpcRoutingInterceptor(settings, services, serviceDef, nextInterceptor.LocalTarget, nextInterceptor.AssumeConnected)
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
            // Note that most likely we intercept a command call here,
            // since this interceptor is used only for non-compute method calls.

            if (!Invalidation.IsActive) // No invalidation -> bypass the call
                return nextHandler.Invoke(invocation);

            // If we're here, the invalidation is active
            if (!rpcMethodDef.Service.HasServer) // The service is an RPC client
                throw Errors.RpcDisabled();

            // If we're here, the service is either an RPC server or a distributed service / service pair.
            // To invalidate every host, we reroute the call to the local target.
            if (localCallAsyncInvoker == null)
                throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

            var resultTask = localCallAsyncInvoker.Invoke(invocation);
            return rpcMethodDef.UniversalAsyncResultWrapper.Invoke(resultTask);
        };
    }
}
