namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcServiceBase(IServiceProvider services) : IHasServices
{
    private RpcHub? _hub;
    private ILogger? _log;

    protected ILogger Log => _log ??= Services.LogFor(GetType());

    public IServiceProvider Services { get; } = services;

    public RpcHub Hub {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _hub ??= Services.RpcHub();
    }
}
