using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public class RpcClientPeer : RpcPeer
{
    private volatile AsyncState<Moment> _reconnectAt = new(default, true);

    public Symbol ClientId { get; protected init; }
    public RpcClientConnectionFactory ConnectionFactory { get; init; }
    public RpcClientPeerReconnectDelayer ReconnectDelayer { get; init; }

    public AsyncState<Moment> ReconnectsAt => _reconnectAt;

    public RpcClientPeer(RpcHub hub, RpcPeerRef @ref, VersionSet? versions = null)
        : base(hub, @ref, versions)
    {
        ClientId = Id.ToBase64Url();
        ConnectionFactory = Hub.ClientConnectionFactory;
        ReconnectDelayer = Hub.ClientPeerReconnectDelayer;
    }

    // Protected methods

    protected override async Task<RpcConnection> GetConnection(
        RpcPeerConnectionState connectionState,
        CancellationToken cancellationToken)
    {
        var delay = ReconnectDelayer.GetDelay(this, connectionState.TryIndex, connectionState.Error, cancellationToken);
        if (delay.IsLimitExceeded)
            throw Errors.ConnectionUnrecoverable();

        SetReconnectsAt(delay.EndsAt);
        try {
            await delay.Task.ConfigureAwait(false);
        }
        finally {
            SetReconnectsAt(default);
        }

        Log.LogInformation("'{PeerRef}': Connecting...", Ref);
        return await ConnectionFactory.Invoke(this, cancellationToken).ConfigureAwait(false);
    }

    protected void SetReconnectsAt(Moment value)
    {
        lock (Lock) {
            if (_reconnectAt.Value != value)
                _reconnectAt = _reconnectAt.SetNext(value);
        }
    }
}
