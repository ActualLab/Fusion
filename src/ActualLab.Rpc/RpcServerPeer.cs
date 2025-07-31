using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcServerPeer(RpcHub hub, RpcPeerRef peerRef, VersionSet? versions = null)
    : RpcPeer(hub, peerRef, versions)
{
    private volatile AsyncState<RpcConnection?> _nextConnection = new(null);

    public void SetConnection(RpcConnection connection)
    {
        AsyncState<RpcPeerConnectionState> connectionState;
        lock (Lock) {
            connectionState = ConnectionState;
            if (connectionState.Value.Connection == connection)
                return; // Already using connection
            if (_nextConnection.Value == connection)
                return; // Already "scheduled" to use connection

            _nextConnection = _nextConnection.SetNext(connection);
        }
        _ = Disconnect(true, null, connectionState);
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
