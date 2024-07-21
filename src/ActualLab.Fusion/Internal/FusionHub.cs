using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Internal;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Interception;
using ActualLab.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Internal;

#pragma warning disable CS9124

public sealed class FusionHub(IServiceProvider services) : IHasServices
{
    private readonly LazySlim<IServiceProvider, IRemoteComputedCache?> _remoteComputedCacheLazy
        = new(services, c => c.GetService<IRemoteComputedCache>());
    private ComputeServiceInterceptor? _interceptor;

    internal readonly RemoteComputeServiceInterceptor.Options RemoteComputeServiceInterceptorOptions
        = services.GetRequiredService<RemoteComputeServiceInterceptor.Options>();
    internal readonly RemoteComputeCallOptions RemoteComputeCallOptions
        = services.GetRequiredService<RemoteComputeCallOptions>();

    public IServiceProvider Services { get; } = services;
    public RpcHub RpcHub { get; } = services.RpcHub();
    public CommanderHub CommanderHub { get; } = services.Commander().Hub;
    public MomentClockSet Clocks { get; } = services.Clocks();
    public IRemoteComputedCache? RemoteComputedCache => _remoteComputedCacheLazy.Value;

    public ComputedOptionsProvider ComputedOptionsProvider { get; }
        = services.GetRequiredService<ComputedOptionsProvider>();
    public ComputeServiceInterceptor Interceptor
        => _interceptor ??= Services.GetRequiredService<ComputeServiceInterceptor>();

    public IProxy NewProxy(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool initialize = true)
    {
        var interceptor = Interceptor;
        interceptor.ValidateType(serviceType);
        return Services.ActivateProxy(serviceType, interceptor, null, initialize);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public IProxy NewClientProxy(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyBaseType,
        object? localTarget,
        bool initialize = true)
    {
        var interceptor = NewClientInterceptor(serviceType, localTarget);
        interceptor.ValidateType(serviceType);
        return Services.ActivateProxy(proxyBaseType, interceptor, localTarget, initialize);
    }

    public RemoteComputeServiceInterceptor NewClientInterceptor(Type serviceType, object? localTarget)
    {
        var rpcInternalServices = RpcHub.InternalServices;
        var nonComputeCallRpcInterceptor = rpcInternalServices.NewRoutingInterceptor(serviceType, CommanderHub.Interceptor);
        var computeCallRpcInterceptor = rpcInternalServices.NewNonRoutingInterceptor(serviceType);
        return new(RemoteComputeServiceInterceptorOptions, this,
            nonComputeCallRpcInterceptor,
            computeCallRpcInterceptor,
            localTarget);
    }
}
