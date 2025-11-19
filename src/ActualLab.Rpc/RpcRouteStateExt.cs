namespace ActualLab.Rpc;

public static class RpcRouteStateExt
{
    extension(RpcRouteState? routeState)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsChanged()
            => routeState is not null && routeState.ChangedToken.IsCancellationRequested;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RerouteIfChanged()
        {
            if (routeState.IsChanged())
                throw RpcRerouteException.MustReroute();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task WhenChanged()
            => routeState is null
                ? TaskExt.NewNeverEndingUnreferenced()
                : TaskExt.NeverEnding(routeState.ChangedToken).SuppressExceptions();

        public Task WhenChanged(CancellationToken cancellationToken)
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
        public RpcShardRouteState? AsShardRouteState(RpcMethodDef methodDef)
            => methodDef.OutboundCallShardRoutingMode is RpcShardRoutingMode.Unused
                ? null
                : routeState as RpcShardRouteState;
    }
}
