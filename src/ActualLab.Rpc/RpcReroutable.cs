using System.Runtime.CompilerServices;

namespace ActualLab.Rpc;

public sealed class RpcRouteState
{
    // RerouteToken must always be cancellable per contract
    public CancellationToken RerouteToken { get; }

    public bool IsRerouted => RerouteToken.IsCancellationRequested;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ThrowIfRerouted()
    {
        if (IsRerouted)
            throw RpcRerouteException.MustReroute();
    }

    public async Task WhenRerouted()
        => await TaskExt.NeverEnding(RerouteToken).SilentAwait(false);

    public Task WhenRerouted(CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
            return WhenRerouted();

        return WhenReroutedWithCancellationToken(cancellationToken);

        async Task WhenReroutedWithCancellationToken(CancellationToken cancellationToken1) {
            using var commonCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken1, RerouteToken);
            await TaskExt.NeverEnding(commonCts.Token).SilentAwait(false);
            cancellationToken1.ThrowIfCancellationRequested();
        }
    }

    public RpcRouteState(CancellationToken rerouteToken)
    {
        RerouteToken = rerouteToken;
#if DEBUG
        // In debug, assert it's cancellable per the assumption in the task
        if (!RerouteToken.CanBeCanceled)
            throw new ArgumentException("RerouteToken must be cancellable", nameof(rerouteToken));
#endif
    }
}
