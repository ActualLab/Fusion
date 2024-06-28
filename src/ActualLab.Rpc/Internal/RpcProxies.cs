using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Rpc.Internal;

public static class RpcProxies
{
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IProxy New(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool initialize = true)
        => New(services, serviceType, serviceType, initialize);

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IProxy New(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyBaseType,
        bool initialize = true)
    {
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];

        var interceptor = rpcHub.InternalServices.NewInterceptor(serviceDef);
        return services.ActivateProxy(proxyBaseType, interceptor, null, initialize);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IProxy NewHybrid(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        bool initialize = true)
    {
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];

        var interceptor = rpcHub.InternalServices.NewInterceptor(serviceDef);
        return services.ActivateProxy(implementationType, interceptor, null, initialize);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IProxy NewSwitch(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool initialize = true)
    {
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];

        var interceptor = rpcHub.InternalServices.NewInterceptor(serviceDef);
        var switchInterceptor = rpcHub.InternalServices.NewSwitchInterceptor(serviceDef, null, interceptor);
        return services.ActivateProxy(serviceType, switchInterceptor, null, initialize);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IProxy NewSwitch(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        ServiceResolver localTargetResolver,
        bool initialize = true)
    {
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];
        var localTarget = localTargetResolver.Resolve(services);

        var interceptor = rpcHub.InternalServices.NewInterceptor(serviceDef);
        var switchInterceptor = rpcHub.InternalServices.NewSwitchInterceptor(serviceDef, localTarget, interceptor);
        return services.ActivateProxy(serviceType, switchInterceptor, null, initialize);
    }
}
