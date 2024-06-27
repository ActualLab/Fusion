using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Rpc.Internal;

public static class RpcProxies
{
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IProxy NewProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool initialize = true)
        => NewProxy(services, serviceType, serviceType, initialize);

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IProxy NewProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyBaseType,
        bool initialize = true)
    {
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];
        var localService = serviceDef.ServerResolver?.Resolve(services);

        var interceptor = rpcHub.InternalServices.NewInterceptor(serviceDef, localService);
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

        var interceptor = rpcHub.InternalServices.NewInterceptor(serviceDef, null);
        return services.ActivateProxy(implementationType, interceptor, null, initialize);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IProxy NewSwitchProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        Func<object>? localTargetResolver,
        bool initialize = true)
    {
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];
        var localTarget = localTargetResolver != null
            ? localTargetResolver.Invoke()
            : serviceDef.ServerResolver.Resolve(services);
        localTarget.Require();

        var interceptor = rpcHub.InternalServices.NewInterceptor(serviceDef, null);
        var switchInterceptor = rpcHub.InternalServices.NewSwitchInterceptor(serviceDef, localTarget, interceptor);
        return services.ActivateProxy(serviceType, switchInterceptor, null, initialize);
    }
}
