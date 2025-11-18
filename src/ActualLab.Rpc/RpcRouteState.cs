namespace ActualLab.Rpc;

public class RpcRouteState
{
    public CancellationToken RerouteToken { get; }

    public RpcRouteState(CancellationToken rerouteToken)
    {
        RerouteToken = rerouteToken;
#if DEBUG
        // RerouteToken must always be cancellable per contract
        if (!RerouteToken.CanBeCanceled)
            throw new ArgumentException("RerouteToken must be cancellable", nameof(rerouteToken));
#endif
    }
}
