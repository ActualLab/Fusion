namespace ActualLab.Rpc;

public class RpcRouteState(CancellationToken changedToken)
{
    public CancellationToken ChangedToken { get; protected set; } = changedToken;
}
