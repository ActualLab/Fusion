using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#if !NET5_0
[RequiresUnreferencedCode(UnreferencedCode.Rpc)]
#endif
public abstract class RpcInterceptorBase : Interceptor
{
    public new record Options : Interceptor.Options;

    public readonly RpcHub Hub;
    public readonly RpcServiceDef ServiceDef;

    // ReSharper disable once ConvertToPrimaryConstructor
    protected RpcInterceptorBase(Options settings, IServiceProvider services, RpcServiceDef serviceDef)
        : base(settings, services)
    {
        Hub = services.RpcHub();
        ServiceDef = serviceDef;
    }

    public override MethodDef? GetMethodDef(MethodInfo method, Type proxyType)
        => ServiceDef.GetOrFindMethod(method);

    protected override MethodDef? CreateMethodDef(MethodInfo method, Type proxyType)
        => ServiceDef.GetOrFindMethod(method);
}
