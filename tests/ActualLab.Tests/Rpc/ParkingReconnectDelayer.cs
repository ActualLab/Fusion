using ActualLab.Net;
using ActualLab.Rpc;

namespace ActualLab.Tests.Rpc;

/// <summary>
/// A <see cref="RpcClientPeerReconnectDelayer"/> that can park every reconnect attempt
/// for <see cref="ParkDelay"/>, the way a client does while the OS reports it offline.
/// </summary>
public sealed class ParkingReconnectDelayer(IServiceProvider services) : RpcClientPeerReconnectDelayer(services)
{
    private long _parkDelayTicks = -1;

    // Read from the peer's reconnect loop, hence the volatile accessors; -1 ticks = not parked
    public TimeSpan? ParkDelay {
        get => Volatile.Read(ref _parkDelayTicks) is var ticks and >= 0 ? TimeSpan.FromTicks(ticks) : null;
        set => Volatile.Write(ref _parkDelayTicks, value?.Ticks ?? -1);
    }

    public override RetryDelay GetDelay(int tryIndex, CancellationToken cancellationToken = default)
    {
        if (ParkDelay is not { } parkDelay)
            return base.GetDelay(tryIndex, cancellationToken);

        // Same cancellable delay as RetryDelayer.GetDelay, but for the very first attempt too
        var cancelDelaysToken = CancelDelaysToken;
        return (DelayImpl(), Clock.Now + parkDelay);

        async Task DelayImpl() {
            using var commonCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancelDelaysToken);
            try {
                await Clock.Delay(parkDelay, commonCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancelDelaysToken.IsCancellationRequested) {
                // Un-parked
            }
        }
    }
}
