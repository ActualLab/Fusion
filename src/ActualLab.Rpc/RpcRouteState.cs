namespace ActualLab.Rpc;

public class RpcRouteState
{
    public CancellationToken ChangedToken { get; }

    public RpcRouteState(CancellationToken changedToken)
    {
        ChangedToken = changedToken;
#if DEBUG
        // RerouteToken must always be cancellable per contract
        if (!RerouteToken.CanBeCanceled)
            throw new ArgumentException("RerouteToken must be cancellable", nameof(rerouteToken));
#endif
    }
}
