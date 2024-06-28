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
    private readonly LazySlim<IServiceProvider, IClientComputedCache?> _clientComputedCacheLazy
        = new(services, c => c.GetService<IClientComputedCache>());
    private CommandServiceInterceptor? _commandServiceInterceptor;
    private ComputeServiceInterceptor? _computeServiceInterceptor;
    private CommandServiceInterceptor.Options? _commandServiceInterceptorOptions;
    private HybridComputeServiceInterceptor.Options? _clientComputeServiceInterceptorOptions;

    internal CommandServiceInterceptor.Options CommandServiceInterceptorOptions
        => _commandServiceInterceptorOptions ??= Services.GetRequiredService<CommandServiceInterceptor.Options>();
    internal HybridComputeServiceInterceptor.Options ClientComputeServiceInterceptorOptions
        => _clientComputeServiceInterceptorOptions ??= Services.GetRequiredService<HybridComputeServiceInterceptor.Options>();

    public IServiceProvider Services { get; } = services;
    public RpcHub RpcHub { get; } = services.RpcHub();
    public MomentClockSet Clocks { get; } = services.Clocks();
    public IClientComputedCache? ClientComputedCache => _clientComputedCacheLazy.Value;

    public ComputedOptionsProvider ComputedOptionsProvider { get; }
        = services.GetRequiredService<ComputedOptionsProvider>();
    public CommandServiceInterceptor CommandServiceInterceptor
        => _commandServiceInterceptor ??= Services.GetRequiredService<CommandServiceInterceptor>();
    public ComputeServiceInterceptor ComputeServiceInterceptor
        => _computeServiceInterceptor ??= Services.GetRequiredService<ComputeServiceInterceptor>();

    public RpcInterceptor NewRpcInterceptor(RpcServiceDef serviceDef)
        => RpcHub.InternalServices.NewInterceptor(serviceDef, CommandServiceInterceptor);
    public HybridComputeServiceInterceptor NewHybridComputeServiceInterceptor(RpcServiceDef serviceDef)
        => new(ClientComputeServiceInterceptorOptions, Services, NewRpcInterceptor(serviceDef));
}
