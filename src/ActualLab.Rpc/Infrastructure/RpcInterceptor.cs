using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcInterceptor : Interceptor
{
    public readonly RpcHub Hub;
    public readonly RpcServiceDef ServiceDef;
    public readonly RpcInterceptorOptions Settings;

    // ReSharper disable once ConvertToPrimaryConstructor
    protected RpcInterceptor(RpcInterceptorOptions settings, IServiceProvider services, RpcServiceDef serviceDef)
        : base(settings, services)
    {
        Hub = services.RpcHub();
        ServiceDef = serviceDef;
        Settings = settings;
        UsesUntypedHandlers = true;
    }

    public override MethodDef? GetMethodDef(MethodInfo method, Type proxyType)
        => ServiceDef.GetOrFindMethod(method);

    protected override MethodDef? CreateMethodDef(MethodInfo method,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyType)
        => ServiceDef.GetOrFindMethod(method);
}
