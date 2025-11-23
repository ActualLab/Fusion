namespace ActualLab.Rpc;

/// <summary>
/// Local execution mode for <see cref="RpcServiceMode.Distributed"/> RPC service methods.
/// Non-distributed RPC services ignore the value of this enum.
/// </summary>
/// <remarks>
/// The resolved mode is stored in <see cref="RpcMethodDef.LocalExecutionMode"/>,
/// you can use <see cref="RpcOutboundCallOptions.LocalExecutionModeResolver"/> and
/// <see cref="RpcMethodAttribute.LocalExecutionMode"/> to override it.
/// </remarks>
public enum RpcLocalExecutionMode
{
    /// <summary>
    /// Default mode (check the context to see what it resolves to).
    /// </summary>
    Default = 0,

    /// <summary>
    /// <see cref="RpcShardRouteState.ShardLockAwaiter"/> is unused.
    /// </summary>
    Unconstrained,

    /// <summary>
    /// <see cref="RpcShardRouteState.ShardLockAwaiter"/> is used to await for the shard lock acquisition.
    /// The cancellation token returned by the awaiter is linked to the original cancellation token
    /// to enforce instant abort on release with subsequent re-lock and reprocessing.
    /// The lock is used only if <see cref="RpcPeerRef.RouteState"/> is <see cref="RpcShardRouteState"/>,
    /// i.e., when a peer offers a way to acquire this lock.
    /// So "Require" here actually means "Prefer", and that's because it's up to the
    /// <see cref="RpcOutboundCallOptions.RouterFactory"/> to return either the lock-enabled <see cref="RpcPeerRef"/>-s
    /// (with <see cref="RpcShardRouteState"/>) or the regular ones (with <see cref="RpcRouteState"/>).
    /// And if it returns the latter, the execution is performed as if <see cref="Unconstrained"/> mode is used.
    /// </summary>
    RequireShardLock,
}

public static class RpcLocalExecutionModeExt
{
    public static RpcLocalExecutionMode Or(this RpcLocalExecutionMode mode, RpcLocalExecutionMode modeIfDefault)
        => mode == RpcLocalExecutionMode.Default ? modeIfDefault : mode;
}
