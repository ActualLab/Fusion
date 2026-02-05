namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Base class for RPC services that provides access to the DI container and <see cref="RpcHub"/>.
/// </summary>
public abstract class RpcServiceBase(IServiceProvider services) : IHasServices
{
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public IServiceProvider Services { get; } = services;

    public RpcHub Hub {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => field ??= Services.RpcHub();
    }
}
