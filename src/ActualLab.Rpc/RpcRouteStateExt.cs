namespace ActualLab.Rpc;

public static class RpcRouteStateExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsChanged(this RpcRouteState? routeState)
        => routeState is not null && routeState.ChangedToken.IsCancellationRequested;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RerouteIfChanged(this RpcRouteState? routeState)
    {
        if (routeState.IsChanged())
            throw RpcRerouteException.MustReroute();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task WhenChanged(this RpcRouteState? routeState)
        => routeState is null
            ? TaskExt.NewNeverEndingUnreferenced()
            : TaskExt.NeverEnding(routeState.ChangedToken).SuppressExceptions();

    public static Task WhenChanged(this RpcRouteState? routeState, CancellationToken cancellationToken)
    {
        if (routeState is null)
            return TaskExt.NeverEnding(cancellationToken);

        return cancellationToken.CanBeCanceled
            ? WhenChangedWithCancellationToken(routeState, cancellationToken)
            // ReSharper disable once MethodSupportsCancellation
            : routeState.WhenChanged();

        static async Task WhenChangedWithCancellationToken(RpcRouteState self, CancellationToken cancellationToken) {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, self.ChangedToken);
            await TaskExt.NeverEnding(linkedCts.Token).SilentAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcShardRouteState? AsShardRouteState(this RpcRouteState? routeState, RpcMethodDef methodDef)
        => methodDef.LocalExecutionMode is RpcLocalExecutionMode.RequireShardLock
            ? routeState as RpcShardRouteState
            : null;
}
