using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcInterceptorBase : Interceptor
{
    public readonly RpcHub Hub;
    public readonly RpcServiceDef ServiceDef;

    // ReSharper disable once ConvertToPrimaryConstructor
    protected RpcInterceptorBase(RpcInterceptorOptions settings, IServiceProvider services, RpcServiceDef serviceDef)
        : base(settings, services)
    {
        Hub = services.RpcHub();
        ServiceDef = serviceDef;
        UsesUntypedHandlers = true;
    }

    public override MethodDef? GetMethodDef(MethodInfo method, Type proxyType)
        => ServiceDef.GetOrFindMethod(method);

    protected override MethodDef? CreateMethodDef(MethodInfo method,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyType)
        => ServiceDef.GetOrFindMethod(method);
}
