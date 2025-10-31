using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcServiceInterceptor : Interceptor
{
    public readonly RpcHub Hub;
    public readonly RpcServiceDef ServiceDef;

    // ReSharper disable once ConvertToPrimaryConstructor
    protected RpcServiceInterceptor(Options settings, RpcHub hub, RpcServiceDef serviceDef)
        : base(settings, hub.Services)
    {
        Hub = hub;
        ServiceDef = serviceDef;
        UsesUntypedHandlers = true;
    }

    public override MethodDef? GetMethodDef(MethodInfo method, Type proxyType)
        => ServiceDef.GetOrFindMethod(method);

    protected override MethodDef? CreateMethodDef(MethodInfo method,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyType)
        => ServiceDef.GetOrFindMethod(method);
}
