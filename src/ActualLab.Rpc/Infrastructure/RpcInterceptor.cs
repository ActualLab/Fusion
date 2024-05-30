using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#if !NET5_0
[RequiresUnreferencedCode(UnreferencedCode.Rpc)]
#endif
public abstract class RpcInterceptor : Interceptor
{
    public new record Options : Interceptor.Options;

    public readonly RpcHub Hub;
    public readonly RpcServiceDef ServiceDef;

    // ReSharper disable once ConvertToPrimaryConstructor
    protected RpcInterceptor(Options settings, IServiceProvider services, RpcServiceDef serviceDef)
        : base(settings, services)
    {
        Hub = services.RpcHub();
        ServiceDef = serviceDef;
    }

    // We don't need to decorate this method with any dynamic access attributes
    protected override MethodDef? CreateMethodDef(MethodInfo method, Type proxyType)
        => ServiceDef.GetMethod(method);
}
