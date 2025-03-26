using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcClientPeer : RpcPeer
{
    private volatile AsyncState<Moment> _reconnectAt = new(default);

    public Symbol ClientId { get; protected init; }
    public RpcClientPeerReconnectDelayer ReconnectDelayer { get; init; }

    public AsyncState<Moment> ReconnectsAt => _reconnectAt;

    public RpcClientPeer(RpcHub hub, RpcPeerRef peerRef, VersionSet? versions = null)
        : base(hub, peerRef, versions)
    {
        ClientId = Id.ToBase64Url();
        ReconnectDelayer = Hub.ClientPeerReconnectDelayer;
    }

    // Protected methods

    protected override async Task<RpcConnection> GetConnection(
        RpcPeerConnectionState connectionState,
        CancellationToken cancellationToken)
    {
        var delay = ReconnectDelayer.GetDelay(this, connectionState.TryIndex, connectionState.Error, cancellationToken);
        if (delay.IsLimitExceeded)
            throw RpcReconnectFailedException.ReconnectFailed(Ref);

        SetReconnectsAt(delay.EndsAt);
        try {
            await delay.Task.ConfigureAwait(false);
        }
        finally {
            SetReconnectsAt(default);
        }

        Log.LogInformation("'{PeerRef}': Connecting...", Ref);
        return await Hub.Client.Connect(this, cancellationToken).ConfigureAwait(false);
    }

    protected void SetReconnectsAt(Moment value)
    {
        lock (Lock) {
            if (_reconnectAt.Value != value)
                _reconnectAt = _reconnectAt.SetNext(value);
        }
    }
}
