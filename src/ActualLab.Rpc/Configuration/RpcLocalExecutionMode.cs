namespace ActualLab.Rpc;

/// <summary>
/// Local execution mode for <see cref="RpcServiceMode.Distributed"/> RPC service methods.
/// Non-distributed RPC servers (services exposed via RPC) don't use call routing
/// and ignore the value of this enum.
/// </summary>
/// <remarks>
/// The resolved mode is stored in <see cref="RpcMethodDef.LocalExecutionMode"/>,
/// you can use <see cref="RpcMethodAttribute.LocalExecutionMode"/> to override it.
/// </remarks>
public enum RpcLocalExecutionMode
{
    /// <summary>
    /// Default mode (check the context to see what it resolves to).
    /// </summary>
    Default = 0,

    /// <summary>
    /// <see cref="RpcRouteState.LocalExecutionAwaiter"/> isn't used,
    /// the cancellation token passed to the local call invoker is the original cancellation token.
    /// This mode is implicitly used for any call that is routed to an <see cref="RpcPeerRef"/>
    /// with a <see cref="RpcPeerRef.RouteState"/> with <c>null</c> <see cref="RpcRouteState.LocalExecutionAwaiter"/>.
    /// </summary>
    Unconstrained = 1,

    /// <summary>
    /// <see cref="RpcRouteState.LocalExecutionAwaiter"/> is awaited before the local call execution.
    /// </summary>
    ConstrainedEntry = 0x10,

    /// <summary>
    /// <see cref="RpcRouteState.LocalExecutionAwaiter"/> is awaited before the local call execution,
    /// and the cancellation token passed to the local call invoker is linked to the
    /// <see cref="RpcRouteState.ChangedToken"/> to enforce instant abort on rerouting.
    /// </summary>
    Constrained = ConstrainedEntry | 0x20,
}

public static class RpcLocalExecutionModeExt
{
    public static RpcLocalExecutionMode Or(this RpcLocalExecutionMode mode, RpcLocalExecutionMode modeIfDefault)
        => mode == RpcLocalExecutionMode.Default ? modeIfDefault : mode;
}
