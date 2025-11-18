namespace ActualLab.Rpc;

public static class RpcRouteStateExt
{
    extension(RpcRouteState? routeState)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRerouted()
            => routeState is not null && routeState.RerouteToken.IsCancellationRequested;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ThrowIfRerouted()
        {
            if (routeState.IsRerouted())
                throw RpcRerouteException.MustReroute();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task WhenRerouted()
            => routeState is null
                ? TaskExt.NewNeverEndingUnreferenced()
                : TaskExt.NeverEnding(routeState.RerouteToken).SuppressExceptions();

        public Task WhenRerouted(CancellationToken cancellationToken)
        {
            if (routeState is null)
                return TaskExt.NeverEnding(cancellationToken);

            return cancellationToken.CanBeCanceled
                ? WhenReroutedWithCancellationToken(routeState, cancellationToken)
                // ReSharper disable once MethodSupportsCancellation
                : routeState.WhenRerouted();

            static async Task WhenReroutedWithCancellationToken(RpcRouteState self, CancellationToken cancellationToken) {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, self.RerouteToken);
                await TaskExt.NeverEnding(linkedCts.Token).SilentAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
