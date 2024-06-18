using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Rpc.Internal;

public static class RpcProxies
{
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IProxy NewClientProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type? proxyBaseType = null,
        bool initialize = true)
    {
        proxyBaseType ??= serviceType;
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];
        var localService = serviceDef.ServerResolver?.Resolve(services);

        var interceptor = rpcHub.InternalServices.NewClientInterceptor(serviceDef, localService);
        return services.ActivateProxy(proxyBaseType, interceptor, null, initialize);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IProxy NewHybridProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        Interceptor? localInterceptor = null,
        bool initialize = true)
    {
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];
        var localService = serviceDef.ServerResolver?.Resolve(services);

        var clientInterceptor = rpcHub.InternalServices.NewClientInterceptor(serviceDef, null);
        var routingInterceptor = rpcHub.InternalServices.NewRoutingInterceptor(serviceDef, localService, clientInterceptor);
        return services.ActivateProxy(implementationType, routingInterceptor, null, initialize);
    }
}
