using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Internal;

public static class RpcProxies
{
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IProxy NewClientProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type? proxyType = null,
        bool isHybrid,
        bool initialize = true)
    {
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];
        proxyType ??= serviceType;

        var interceptorOptions = services.GetRequiredService<RpcClientInterceptor.Options>();
        var interceptor = new RpcClientInterceptor(interceptorOptions, services, serviceDef) {
            IsHybrid = isHybrid,
        };
        return services.ActivateProxy(proxyType, interceptor, null, initialize);
    }
}
