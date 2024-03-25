using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Internal;

public static class RpcProxies
{
    public static object NewClientProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type? proxyType = null)
    {
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];
        proxyType ??= serviceType;

        var interceptor = services.GetRequiredService<RpcClientInterceptor>();
        interceptor.Setup(serviceDef);
        var proxy = Proxies.New(proxyType, interceptor);
        return proxy;
    }

    public static object NewSwitchProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        ServiceResolver localServiceResolver)
    {
        var rpcHub = services.RpcHub();
        var localService = localServiceResolver.Resolve(services);
        var client = NewClientProxy(services, serviceType);
        var serviceDef = rpcHub.ServiceRegistry[serviceType];

        var interceptor = services.GetRequiredService<RpcSwitchInterceptor>();
        interceptor.Setup(serviceDef, localService, client);
        var proxy = Proxies.New(serviceType, interceptor);
        return proxy;
    }
}
