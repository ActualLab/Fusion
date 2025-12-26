using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcServerPeer(RpcHub hub, RpcPeerRef peerRef, VersionSet? versions = null)
    : RpcPeer(hub, peerRef, versions)
{
    private volatile AsyncState<RpcConnection?> _nextConnection = new(null);

    public async Task SetNextConnection(RpcConnection connection, CancellationToken cancellationToken = default)
    {
        AsyncState<RpcPeerConnectionState> connectionState;
        lock (Lock) {
            connectionState = ConnectionState;
            if (_nextConnection.Value == connection)
                return; // We already set the next connection to the specified one

            _nextConnection = _nextConnection.SetNext(connection);
        }
        await Disconnect(null, connectionState, cancellationToken).ConfigureAwait(false);
    }

    // Protected methods

    protected override async Task<RpcConnection> GetConnection(
        RpcPeerConnectionState connectionState,
        CancellationToken cancellationToken)
    {
        while (true) {
            AsyncState<RpcConnection?> nextConnection;
            lock (Lock) {
                nextConnection = _nextConnection;
                var connection = nextConnection.Value;
                if (connection is not null) {
                    _nextConnection = nextConnection.SetNext(null); // This allows SetConnection to work properly
                    return connection;
                }
            }
            try {
                var closeTimeout = Hub.PeerOptions.ServerPeerShutdownTimeoutProvider.Invoke(this);
                await nextConnection
                    .When(x => x is not null, cancellationToken)
                    .WaitAsync(closeTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException) {
                throw RpcReconnectFailedException.ClientIsGone();
            }
        }
    }
}
