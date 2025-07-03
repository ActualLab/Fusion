using ActualLab.Fusion.Internal;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Interception;

public sealed class DisableRpcWhileInvalidatingInterceptor(
    RpcInterceptorOptions settings,
    IServiceProvider services,
    RpcServiceDef serviceDef,
    RpcInterceptor nextInterceptor)
    : RpcInterceptor(settings, services, serviceDef)
{
    public readonly RpcInterceptor NextInterceptor = nextInterceptor;

    protected override Func<Invocation, object?>? CreateUntypedHandler(Invocation initialInvocation, MethodDef methodDef)
    {
        var nextHandler = NextInterceptor.GetHandler(initialInvocation);
        if (nextHandler is null)
            return null;

        return invocation => Invalidation.IsActive
            ? throw Errors.RpcDisabled()
            : nextHandler.Invoke(invocation);
    }
}
