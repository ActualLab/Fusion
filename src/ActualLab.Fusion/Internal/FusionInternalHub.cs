using ActualLab.CommandR.Interception;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Internal;

#pragma warning disable CS9124

public sealed class FusionInternalHub(IServiceProvider services) : IHasServices
{
    private readonly LazySlim<IServiceProvider, IRemoteComputedCache?> _remoteComputedCacheLazy
        = new(services, c => c.GetService<IRemoteComputedCache>());
    private CommandServiceInterceptor? _commandServiceInterceptor;
    private ComputeServiceInterceptor? _computeServiceInterceptor;
    private RpcComputeServiceInterceptor.Options? _clientComputeServiceInterceptorOptions;
    private RpcComputeCallOptions? _rpcComputeCallOptions;

    internal RpcComputeServiceInterceptor.Options RpcComputeServiceInterceptorOptions
        => _clientComputeServiceInterceptorOptions ??= Services.GetRequiredService<RpcComputeServiceInterceptor.Options>();
    internal RpcComputeCallOptions RpcComputeCallOptions
        => _rpcComputeCallOptions ??= Services.GetRequiredService<RpcComputeCallOptions>();

    public IServiceProvider Services { get; } = services;
    public RpcHub RpcHub { get; } = services.RpcHub();
    public MomentClockSet Clocks { get; } = services.Clocks();
    public IRemoteComputedCache? RemoteComputedCache => _remoteComputedCacheLazy.Value;

    public ComputedOptionsProvider ComputedOptionsProvider { get; }
        = services.GetRequiredService<ComputedOptionsProvider>();
    public CommandServiceInterceptor CommandServiceInterceptor
        => _commandServiceInterceptor ??= Services.GetRequiredService<CommandServiceInterceptor>();
    public ComputeServiceInterceptor ComputeServiceInterceptor
        => _computeServiceInterceptor ??= Services.GetRequiredService<ComputeServiceInterceptor>();

    public RpcComputeServiceInterceptor NewRpcComputeServiceInterceptor(RpcServiceDef serviceDef, object? localTarget)
    {
        var regularCallRpcInterceptor = RpcHub.InternalServices.NewInterceptor(serviceDef, CommandServiceInterceptor);
        var computeCallRpcInterceptor = RpcHub.InternalServices.NewInterceptor(serviceDef); // Shouldn't have localTarget!
        return new(RpcComputeServiceInterceptorOptions, regularCallRpcInterceptor, computeCallRpcInterceptor, localTarget, this);
    }
}
