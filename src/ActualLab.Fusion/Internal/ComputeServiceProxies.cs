using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Interception;
using ActualLab.Interception;

namespace ActualLab.Fusion.Internal;

public static class ComputeServiceProxies
{
    public static IProxy New(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool initialize = true)
    {
        var computeServiceInterceptor = services.GetRequiredService<ComputeServiceInterceptor>();
        computeServiceInterceptor.ValidateType(serviceType);
        return services.ActivateProxy(serviceType, computeServiceInterceptor, null, initialize);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IProxy NewClient(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool initialize = true)
        => NewHybrid(services, serviceType, serviceType, null, initialize);

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IProxy NewClient(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        ServiceResolver? localTargetResolver,
        bool initialize = true)
        => NewHybrid(services, serviceType, serviceType, localTargetResolver, initialize);

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IProxy NewHybrid(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        ServiceResolver? localTargetResolver = null,
        bool initialize = true)
    {
        var hub = services.GetRequiredService<FusionInternalHub>();
        var serviceDef = hub.RpcHub.ServiceRegistry[serviceType];
        var localTarget = localTargetResolver?.Resolve(services);

        var hybridComputeServiceInterceptor = hub.NewRpcComputeServiceInterceptor(serviceDef, localTarget);
        hybridComputeServiceInterceptor.ValidateType(serviceType);
        return services.ActivateProxy(implementationType, hybridComputeServiceInterceptor, localTarget, initialize);
    }
}
