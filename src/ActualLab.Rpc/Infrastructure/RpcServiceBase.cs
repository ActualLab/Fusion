using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcServiceBase(IServiceProvider services) : IHasServices
{
    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public IServiceProvider Services { get; } = services;

    [field: AllowNull, MaybeNull]
    public RpcHub Hub {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => field ??= Services.RpcHub();
    }
}
