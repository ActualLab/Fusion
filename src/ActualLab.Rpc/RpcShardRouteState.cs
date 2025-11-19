namespace ActualLab.Rpc;

public class RpcShardRouteState(
    Func<CancellationToken, ValueTask<CancellationToken>> shardLockAwaiter,
    CancellationToken changeToken
    ) : RpcRouteState(changeToken)
{
    /// <summary>
    /// Returns a function allowing to await for shard lock, i.e., for a moment when
    /// the current process exclusively owns the whole shard the current route is associated with,
    /// so that no other process can access it.
    /// When
    /// </summary>
    /// <remarks>
    /// <para>
    /// You may notice there is no "release" semantic: the ownership awaiter isn't supposed to "lock"
    /// the shard every time it's called. This must be done somewhere else, and the assumption is that
    /// every process always tries to acquire ownership lock for its subset of shards.
    /// </para>
    /// <para>
    /// The <see cref="Task{TResult}"/> returned by the awaiter must complete when the shard is owned exclusively
    /// by the current process. The cancellation token this task returns will be canceled when the ownership is lost.
    /// </para>
    /// <para>
    /// The <see cref="CancellationToken"/> awaiter gets must be used only to cancel the ownership await itself.
    /// It must NOT be linked to the "ownership is lost" token returned by the awaiter.
    /// </para>
    /// </remarks>
    public Func<CancellationToken, ValueTask<CancellationToken>> ShardLockAwaiter { get; protected set; } = shardLockAwaiter;
}
