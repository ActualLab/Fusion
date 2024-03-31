using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Interception.Interceptors;

namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcInterceptorBase(
    RpcInterceptorBase.Options settings,
    IServiceProvider services,
    RpcServiceDef serviceDef
    ) : InterceptorBase(settings, services)
{
    public new record Options : InterceptorBase.Options;

    private RpcHub? _rpcHub;

    public RpcHub Hub => _rpcHub ??= Services.RpcHub();
    public readonly RpcServiceDef ServiceDef = serviceDef;

    // We don't need to decorate this method with any dynamic access attributes
    protected override MethodDef? CreateMethodDef(MethodInfo method, Type proxyType)
        => ServiceDef.Methods.FirstOrDefault(m => m.Method == method);

    protected override void ValidateTypeInternal(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    { }
}
