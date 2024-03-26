using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Internal;

public static class FusionProxies
{
    public static IProxy NewLocalProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool initialize = true)
    {
        var interceptor = services.GetRequiredService<ComputeServiceInterceptor>();
        // We should try to validate it here because if the type doesn't
        // have any virtual methods (which might be a mistake), no calls
        // will be intercepted, so no error will be thrown later.
        interceptor.ValidateType(serviceType);
        var proxy = services.ActivateProxy(serviceType, interceptor, null, initialize);
        return proxy;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IProxy NewClientProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool initialize = true)
    {
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];

        // Creating ClientComputeServiceInterceptor
        var clientInterceptor = new RpcClientInterceptor(
            services.GetRequiredService<RpcClientInterceptor.Options>(), services, serviceDef);
        var interceptor = new ClientComputeServiceInterceptor(
            services.GetRequiredService<ClientComputeServiceInterceptor.Options>(), services, clientInterceptor);
        // We should try to validate it here because if the type doesn't
        // have any virtual methods (which might be a mistake), no calls
        // will be intercepted, so no error will be thrown later.
        interceptor.ValidateType(serviceType);

        // Creating proxy
        var proxy = Proxies.New(serviceType, interceptor, null, initialize);
        return proxy;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IProxy NewHybridProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        ServiceResolver localServiceResolver,
        bool initialize = true)
    {
        var rpcHub = services.RpcHub();
        var serviceDef = rpcHub.ServiceRegistry[serviceType];
        var client = NewClientProxy(services, serviceType);

        // Replacing client's interceptor with RpcHybridInterceptor
        var localService = localServiceResolver.Resolve(services);
        var interceptor = new RpcHybridInterceptor(
            services.GetRequiredService<RpcHybridInterceptor.Options>(), services,
            serviceDef, localService, client, true);
        interceptor.BindTo(client, null, initialize);
        return client;
    }
}
