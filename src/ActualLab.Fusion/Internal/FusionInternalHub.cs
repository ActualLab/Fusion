using ActualLab.CommandR.Interception;
using ActualLab.Fusion.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Internal;

#pragma warning disable CS9124

public sealed class FusionInternalHub(IServiceProvider services) : IHasServices
{
    private CommandServiceInterceptor? _commandServiceInterceptor;
    private ComputeServiceInterceptor? _computeServiceInterceptor;

    public IServiceProvider Services { get; } = services;
    public MomentClockSet Clocks { get; } = services.Clocks();

    public ComputedOptionsProvider ComputedOptionsProvider { get; }
        = services.GetRequiredService<ComputedOptionsProvider>();
    public CommandServiceInterceptor CommandServiceInterceptor
        => _commandServiceInterceptor ??= Services.GetRequiredService<CommandServiceInterceptor>();
    public ComputeServiceInterceptor ComputeServiceInterceptor
        => _computeServiceInterceptor ??= Services.GetRequiredService<ComputeServiceInterceptor>();

    public ConcurrentDictionary<Symbol, RpcPeer> Peers { get; } = new();
}
