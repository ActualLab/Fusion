using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

/// <summary>
/// Represents the client side of an RPC peer connection, handling reconnection logic.
/// </summary>
public class RpcClientPeer : RpcPeer
{
    private AsyncState<Moment> _reconnectAt = new(default);
    private string? _reconnectSecret;
    private long _reconnectCounter;

    public string ClientId { get; protected init; }
    public RpcClientPeerReconnectDelayer ReconnectDelayer { get; init; }

    public AsyncState<Moment> ReconnectsAt => _reconnectAt;
    // In-memory only: never persisted to storage, a cookie, or a URL. Losing it costs nothing,
    // since a new RpcClientPeer also mints a new Id and thus a new ClientId.
    public string? ReconnectSecret => _reconnectSecret;

    public RpcClientPeer(RpcHub hub, RpcRoute route, VersionSet? versions = null)
        : base(hub, route, versions)
    {
        ClientId = Id.ToBase64Url();
        ReconnectDelayer = Hub.ClientPeerReconnectDelayer;
    }

    // Called once per connect attempt, not once per successful connection: an attempt the server
    // admitted at its gate has already burned that counter value, so reusing it would lock the
    // client out of every further reconnect.
    public long NextReconnectCounter()
        => Interlocked.Increment(ref _reconnectCounter);

    // Protected methods

    protected override void OnHandshake(RpcHandshake handshake)
    {
        // Overwrites unconditionally: the server sends its secret on every handshake, so a client
        // that missed one self-heals, and a client that reached another server instance adopts
        // that instance's secret. A null secret (legacy server) leaves the stored one intact.
        if (handshake.Secret is { Length: > 0 } secret)
            Volatile.Write(ref _reconnectSecret, secret); // Published to lock-free ReconnectSecret readers
    }

    protected override async Task<RpcConnection> GetConnection(
        RpcPeerConnectionState connectionState,
        CancellationToken cancellationToken)
    {
        if (Ref.ConnectionKind is RpcPeerConnectionKind.None)
            throw RpcReconnectFailedException.ReconnectFailed(Ref);

        var delay = ReconnectDelayer.GetDelay(this, connectionState.ConnectionAttemptIndex, connectionState.Error, cancellationToken);
        if (delay.IsLimitExceeded)
            throw RpcReconnectFailedException.ReconnectFailed(Ref);

        SetReconnectsAt(delay.EndsAt);
        try {
            await delay.Task.ConfigureAwait(false);
        }
        finally {
            SetReconnectsAt(default);
        }

        Log.LogInformation("'{Route}': Connecting...", Route);
        return await Hub.Client.Connect(this, connectionState, cancellationToken).ConfigureAwait(false);
    }

    protected void SetReconnectsAt(Moment value)
    {
        lock (Lock) {
            if (_reconnectAt.Value != value)
                Volatile.Write(ref _reconnectAt, _reconnectAt.SetNext(value));
        }
    }
}
