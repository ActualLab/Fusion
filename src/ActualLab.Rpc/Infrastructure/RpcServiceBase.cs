namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcServiceBase(IServiceProvider services) : IHasServices
{
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public IServiceProvider Services { get; } = services;

    public RpcHub Hub {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => field ??= Services.RpcHub();
    }
}
