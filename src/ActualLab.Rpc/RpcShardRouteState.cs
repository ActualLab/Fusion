namespace ActualLab.Rpc;

public abstract class RpcShardRouteState(CancellationToken changeToken) : RpcRouteState(changeToken)
{
    /// <summary>
    /// Returns a task that completes when shard is owned.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Cancellation token that cancels when shard ownership gets lost.
    /// This token is NOT linked to the provided <paramref name="cancellationToken"/>.
    /// </returns>
    public abstract Task<CancellationToken> WhenShardOwned(CancellationToken cancellationToken);
}
