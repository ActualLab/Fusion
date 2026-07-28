using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

/// <summary>
/// Represents the server side of an RPC peer connection, waiting for incoming connections.
/// </summary>
public class RpcServerPeer(RpcHub hub, RpcRoute route, VersionSet? versions = null)
    : RpcPeer(hub, route, versions)
{
    private volatile AsyncState<RpcConnection?> _nextConnection = new(null);
    private long _lastCounter;

    // Minted here, so it exists before any handshake and is stable for the peer's whole lifetime.
    // Secret and _lastCounter are both instance state with no reset path, which is what makes
    // "the counter never resets while the paired secret survives" true by construction.
    public string Secret { get; } = RpcReconnectProof.NewSecret();
    public long LastCounter => Interlocked.Read(ref _lastCounter);

    public async Task SetNextConnection(RpcConnection connection, CancellationToken cancellationToken = default)
    {
        RpcConnection? oldQueuedConnection;
        lock (Lock) {
            if (_nextConnection.Value == connection)
                return; // We already set the next connection to the specified one

            oldQueuedConnection = _nextConnection.Value;
            _nextConnection = _nextConnection.SetNext(connection);
        }
        // Dispose any previously-queued connection we just overwrote (it would otherwise
        // leak its WebSocket — its Invoke caller is parked on transport.WhenClosed).
        if (oldQueuedConnection is not null && !ReferenceEquals(oldQueuedConnection, connection))
            _ = oldQueuedConnection.DisposeAsync();

        // Disconnect any IN-FLIGHT connection that isn't ours. We can't pin an
        // expectedState (the previous connection's handshake may complete in the gap
        // between the lock release above and Disconnect's lock acquisition; that path
        // would leave our connection queued behind a now-Connected old one). Instead
        // re-check under Disconnect's own lock. Only the connection that is still the
        // queued winner may cancel stale in-flight work; if a newer accept overwrote
        // ours, that newer SetNextConnection call owns the teardown.
        while (true) {
            CancellationTokenSource? oldReaderTokenSource;
            Task whenDisconnected;
            lock (Lock) {
                var queued = _nextConnection.Value;
                var connectionState = ConnectionState;
                var inFlight = connectionState.Value.Connection;
                if (inFlight is null || ReferenceEquals(inFlight, connection))
                    return; // Nothing to disconnect, or OnRun picked up ours
                if (!ReferenceEquals(queued, connection)) {
                    // Ours was superseded by a newer accept. Returning success here would be wrong:
                    // local/test clients treat SetNextConnection success as "the server side is
                    // accepted" and start handshaking on a connection the server will never consume.
                    // Report it as closed so the client connect attempt retries instead.
                    throw new ChannelClosedException();
                }

                oldReaderTokenSource = connectionState.Value.ReaderTokenSource;
                whenDisconnected = connectionState.Value.WhenDisconnected;
            }
            oldReaderTokenSource?.CancelAndDisposeSilently();
            await whenDisconnected.WaitAsync(cancellationToken).ConfigureAwait(false);
            // Re-check: another stale connection may have raced in.
        }
    }

    public bool TryAdvanceCounter(long counter)
    {
        // A CAS loop rather than lock (Lock): this runs on the pre-auth request path, before the
        // peer is otherwise touched, so it must not contend with SetNextConnection's lock.
        while (true) {
            var lastCounter = Interlocked.Read(ref _lastCounter);
            if (counter <= lastCounter)
                return false;
            if (Interlocked.CompareExchange(ref _lastCounter, counter, lastCounter) == lastCounter)
                return true;
        }
    }

    // Protected methods

    protected override RpcHandshake CreateHandshake(int index)
        => base.CreateHandshake(index) with { Secret = Secret };

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
