using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Internal;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Interception;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Internal;

#pragma warning disable CS9124

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
    [field: AllowNull, MaybeNull]
    public ILogger RemoteComputedCacheLog => field ??= Services.LogFor<IRemoteComputedCache>();
    public ComputedOptionsProvider ComputedOptionsProvider { get; }
        = services.GetRequiredService<ComputedOptionsProvider>();
    [field: AllowNull, MaybeNull]
    public ComputeServiceInterceptor ComputeServiceInterceptor
        => field ??= Services.GetRequiredService<ComputeServiceInterceptor>();

    public IProxy NewComputeServiceProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool initialize = true)
    {
        var interceptor = ComputeServiceInterceptor;
        interceptor.ValidateType(serviceType);
        return services.ActivateProxy(serviceType, interceptor, null, initialize);
    }

    public IProxy NewRemoteComputeServiceProxy(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyBaseType,
        object? localTarget,
        bool initialize = true)
    {
        var interceptor = NewRemoteComputeServiceInterceptor(serviceType, localTarget);
        interceptor.ValidateType(serviceType);
        return Services.ActivateProxy(proxyBaseType, interceptor, localTarget, initialize);
    }

    public RemoteComputeServiceInterceptor NewRemoteComputeServiceInterceptor(Type serviceType, object? localTarget)
    {
        var rpcInternalServices = RpcHub.InternalServices;
        var computeCallRpcInterceptor = rpcInternalServices.NewNonRoutingInterceptor(serviceType, assumeConnected: true);
        var rpcRoutingInterceptor = rpcInternalServices.NewRoutingInterceptor(serviceType, CommanderHub.Interceptor);
        var nonComputeCallRpcInterceptor = NewDisableRpcWhileInvalidatingInterceptor(rpcRoutingInterceptor);
        return new(RemoteComputeServiceInterceptorOptions, this,
            nonComputeCallRpcInterceptor,
            computeCallRpcInterceptor,
            localTarget);
    }

    public DisableRpcWhileInvalidatingInterceptor NewDisableRpcWhileInvalidatingInterceptor(RpcRoutingInterceptor next)
        => new(RpcHub.InternalServices.InterceptorOptions, Services, next.ServiceDef, next);
}
