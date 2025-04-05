using System.Diagnostics.CodeAnalysis;
using ActualLab.Net;

namespace ActualLab.Rpc;

public class RpcClientPeerReconnectDelayer : RetryDelayer, IHasServices
{
    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public IServiceProvider Services { get; }
    [field: AllowNull, MaybeNull]
    public RpcHub Hub => field ??= Services.RpcHub();

    public RpcClientPeerReconnectDelayer(IServiceProvider services)
    {
        Services = services;
        ClockProvider = () => Hub.Clock; // Hub resolves this service in .ctor, so we can't resolve Hub here
        Delays = RpcDefaults.Mode == RpcMode.Client
            ? RetryDelaySeq.Exp(1, 60)
            : RetryDelaySeq.Exp(0.5, 10);
    }

    public virtual RetryDelay GetDelay(
        RpcClientPeer peer, int tryIndex, Exception? lastError,
        CancellationToken cancellationToken = default)
    {
        var delayLogger = new RetryDelayLogger("reconnect", string.Concat("'", peer.Ref, "'"), Log);
        return this.GetDelay(tryIndex, delayLogger, cancellationToken);
    }
}
