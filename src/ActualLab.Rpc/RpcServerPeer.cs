using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcServerPeer(RpcHub hub, RpcPeerRef peerRef, VersionSet? versions = null)
    : RpcPeer(hub, peerRef, versions)
{
    private volatile AsyncState<RpcConnection?> _nextConnection = new(null);

    public async Task SetConnection(RpcConnection connection, CancellationToken cancellationToken = default)
    {
        while (true) {
            AsyncState<RpcPeerConnectionState> connectionState;
            bool mustDisconnect;
            lock (Lock) {
                connectionState = ConnectionState;
                if (connectionState.Value.Connection == connection)
                    return; // Already using connection
                if (_nextConnection.Value == connection)
                    break;

                _nextConnection = _nextConnection.SetNext(connection);
                mustDisconnect = true;
            }
            if (mustDisconnect)
                await Disconnect(null, connectionState, cancellationToken).ConfigureAwait(false);
            else // Otherwise we just wait for the next connection to happen
                await connectionState.WhenNext(cancellationToken).ConfigureAwait(false);
        }
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
                var closeTimeout = Hub.ServerPeerCloseTimeoutProvider.Invoke(this);
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
