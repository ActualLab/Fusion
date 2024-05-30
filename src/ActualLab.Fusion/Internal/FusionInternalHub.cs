using ActualLab.CommandR.Interception;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Internal;

#pragma warning disable CS9124

public sealed class FusionInternalHub(IServiceProvider services) : IHasServices
{
    private LazySlim<IServiceProvider, IClientComputedCache?> _clientComputedCacheLazy
        = new(services, c => c.GetService<IClientComputedCache>());
    private CommandServiceInterceptor? _commandServiceInterceptor;
    private ComputeServiceInterceptor? _computeServiceInterceptor;
    private ClientComputeServiceInterceptor? _clientComputeServiceInterceptor;

    public IServiceProvider Services { get; } = services;
    public MomentClockSet Clocks { get; } = services.Clocks();
    public IClientComputedCache? ClientComputedCache => _clientComputedCacheLazy.Value;

    public ComputedOptionsProvider ComputedOptionsProvider { get; }
        = services.GetRequiredService<ComputedOptionsProvider>();
    public CommandServiceInterceptor CommandServiceInterceptor
        => _commandServiceInterceptor ??= Services.GetRequiredService<CommandServiceInterceptor>();
    public ComputeServiceInterceptor ComputeServiceInterceptor
        => _computeServiceInterceptor ??= Services.GetRequiredService<ComputeServiceInterceptor>();
    public ClientComputeServiceInterceptor ClientComputeServiceInterceptor
        => _clientComputeServiceInterceptor ??= Services.GetRequiredService<ClientComputeServiceInterceptor>();

    public ConcurrentDictionary<Symbol, RpcPeer> Peers { get; } = new();
}
