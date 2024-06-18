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
    private ClientComputeServiceInterceptor.Options? _clientComputeServiceInterceptorOptions;

    private ClientComputeServiceInterceptor.Options ClientComputeServiceInterceptorOptions
        => _clientComputeServiceInterceptorOptions ??= Services.GetRequiredService<ClientComputeServiceInterceptor.Options>();

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

    public ClientComputeServiceInterceptor NewClientComputeServiceInterceptor(RpcClientInterceptor clientInterceptor)
        => new(ClientComputeServiceInterceptorOptions, Services, clientInterceptor);
}
