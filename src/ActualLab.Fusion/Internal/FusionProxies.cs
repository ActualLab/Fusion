using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Interception;
using ActualLab.Interception;

namespace ActualLab.Fusion.Internal;

public static class FusionProxies
{
    public static IProxy NewProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool initialize = true)
    {
        var computeServiceInterceptor = services.GetRequiredService<ComputeServiceInterceptor>();
        computeServiceInterceptor.ValidateType(serviceType);
        return services.ActivateProxy(serviceType, computeServiceInterceptor, null, initialize);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IProxy NewClientProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool initialize = true)
    {
        var fusionHub = services.GetRequiredService<FusionInternalHub>();
        var rpcHub = fusionHub.RpcHub;
        var serviceDef = rpcHub.ServiceRegistry[serviceType];

        var clientInterceptor = rpcHub.InternalServices.NewClientInterceptor(serviceDef);
        var clientComputeServiceInterceptor = fusionHub.NewClientComputeServiceInterceptor(clientInterceptor);
        clientComputeServiceInterceptor.ValidateType(serviceType);
        return services.ActivateProxy(serviceType, clientComputeServiceInterceptor, null, initialize);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IProxy NewHybridProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        bool initialize = true)
    {
        var fusionHub = services.GetRequiredService<FusionInternalHub>();
        var rpcHub = fusionHub.RpcHub;
        var serviceDef = rpcHub.ServiceRegistry[implementationType];

        var computeServiceInterceptor = services.GetRequiredService<ComputeServiceInterceptor>();
        var clientInterceptor = rpcHub.InternalServices.NewClientInterceptor(serviceDef);
        var clientComputeServiceInterceptor = fusionHub.NewClientComputeServiceInterceptor(clientInterceptor);
        clientComputeServiceInterceptor.ValidateType(implementationType);
        var routingInterceptor = rpcHub.InternalServices.NewRoutingInterceptor(
            serviceDef, computeServiceInterceptor, clientComputeServiceInterceptor);
        return services.ActivateProxy(implementationType, routingInterceptor, null, initialize);
    }
}
