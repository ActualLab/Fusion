using ActualLab.CommandR.Internal;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Interception;
using ActualLab.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Internal;

#pragma warning disable CS9124

/// <summary>
/// A central hub providing access to core Fusion services such as interceptors,
/// computed options, and proxy factories.
/// </summary>
public sealed class FusionHub(IServiceProvider services) : IHasServices
{
    private readonly LazySlim<IServiceProvider, IRemoteComputedCache?> _remoteComputedCacheLazy
        = new(services, c => c.GetService<IRemoteComputedCache>());

    internal readonly RemoteComputeServiceInterceptor.Options RemoteComputeServiceInterceptorOptions
        = services.GetRequiredService<RemoteComputeServiceInterceptor.Options>();

    public IServiceProvider Services { get; } = services;
    public RpcHub RpcHub { get; } = services.RpcHub();
    public CommanderHub CommanderHub { get; } = services.Commander().Hub;
    public MomentClockSet Clocks { get; } = services.Clocks();
    public IRemoteComputedCache? RemoteComputedCache => _remoteComputedCacheLazy.Value;
    public ILogger RemoteComputedCacheLog => field ??= Services.LogFor<IRemoteComputedCache>();
    public ComputedOptionsProvider ComputedOptionsProvider { get; }
        = services.GetRequiredService<ComputedOptionsProvider>();
    public ComputedOutputEqualityComparer ComputedOutputEqualityComparer { get; }
        = services.GetRequiredService<ComputedOutputEqualityComparer>();
    public ComputeServiceInterceptor ComputeServiceInterceptor
        => field ??= Services.GetRequiredService<ComputeServiceInterceptor>();

    public IProxy NewComputeServiceProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool initialize = true)
    {
        var interceptor = ComputeServiceInterceptor;
        interceptor.ValidateType(serviceType);
        // FusionHub.Services always points to the root service provider, and compute services can be Scoped
        return services.ActivateProxy(serviceType, interceptor, initialize);
    }

    public IProxy NewRemoteComputeServiceProxy(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyBaseType,
        bool initialize = true)
    {
        var interceptor = NewRemoteComputeServiceInterceptor(serviceType);
        interceptor.ValidateType(serviceType);
        return Services.ActivateProxy(proxyBaseType, interceptor, initialize);
    }

    public RemoteComputeServiceInterceptor NewRemoteComputeServiceInterceptor(Type serviceType)
    {
        var rpcInternalServices = RpcHub.InternalServices;
        var rpcInterceptor = rpcInternalServices.NewInterceptor(serviceType, CommanderHub.Interceptor);
        return new(RemoteComputeServiceInterceptorOptions, this, rpcInterceptor);
    }
}
