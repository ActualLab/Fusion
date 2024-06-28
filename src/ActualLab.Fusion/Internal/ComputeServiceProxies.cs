using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Interception;
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
    {
        var hub = services.GetRequiredService<FusionInternalHub>();
        var serviceDef = hub.RpcHub.ServiceRegistry[serviceType];

        var hybridComputeServiceInterceptor = hub.NewHybridComputeServiceInterceptor(serviceDef);
        hybridComputeServiceInterceptor.ValidateType(serviceType);
        return services.ActivateProxy(serviceType, hybridComputeServiceInterceptor, null, initialize);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IProxy NewHybrid(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        bool initialize = true)
    {
        var hub = services.GetRequiredService<FusionInternalHub>();
        var serviceDef = hub.RpcHub.ServiceRegistry[serviceType];

        var hybridComputeServiceInterceptor = hub.NewHybridComputeServiceInterceptor(serviceDef);
        hybridComputeServiceInterceptor.ValidateType(serviceType);
        return services.ActivateProxy(implementationType, hybridComputeServiceInterceptor, null, initialize);
    }
}
