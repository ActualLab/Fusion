using System.Diagnostics;

namespace ActualLab.Rpc;

public class RpcRouteState
{
    public CancellationToken ChangedToken { get; }

    public RpcRouteState(CancellationToken changedToken)
    {
        Debug.Assert(changedToken.CanBeCanceled);
        ChangedToken = changedToken;
    }
}
