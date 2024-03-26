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
        bool initialize = true)
    {
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];
        proxyType ??= serviceType;

        var interceptor = new RpcClientInterceptor(
            services.GetRequiredService<RpcClientInterceptor.Options>(), services, serviceDef);
        var proxy = Proxies.New(proxyType, interceptor, initialize);
        return proxy;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IProxy NewHybridProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        ServiceResolver localServiceResolver,
        bool initialize = true)
    {
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];
        var client = NewClientProxy(services, serviceType, initialize: false);

        // Replacing client's interceptor with RpcHybridInterceptor
        var localService = localServiceResolver.Resolve(services);
        var interceptor = new RpcHybridInterceptor(
            services.GetRequiredService<RpcHybridInterceptor.Options>(), services,
            serviceDef, localService, client, true);
        interceptor.BindTo(client, null, initialize);
        return client;
    }
}
