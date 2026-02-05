using ActualLab.Channels;
using ActualLab.Internal;
using ActualLab.IO;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Testing;

/// <summary>
/// Manages a paired client-server in-memory connection for RPC testing with connect/disconnect/reconnect support.
/// </summary>
public class RpcTestConnection
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private volatile AsyncState<ChannelPair<ArrayOwner<byte>>?> _channels = new(null);

    public RpcTestClient TestClient { get; }
    public RpcHub Hub => TestClient.Hub;
    public RpcPeerRef ClientPeerRef { get; }
    public RpcPeerRef ServerPeerRef { get; }
    public RpcClientPeer ClientPeer => field ??= Hub.GetClientPeer(ClientPeerRef);
    public RpcServerPeer ServerPeer => field ??= Hub.GetServerPeer(ServerPeerRef);

    public ChannelPair<ArrayOwner<byte>>? Channels {
        // ReSharper disable once InconsistentlySynchronizedField
        get => _channels.Last.Value;
        protected set {
            lock (_lock) {
                if (_channels.IsFinal)
                    throw Errors.AlreadyDisposed();
                if (ReferenceEquals(_channels.Value, value))
                    return;

                // Complete old channels to stop any active transports
                var oldChannels = _channels.Value;
                if (oldChannels is not null) {
                    oldChannels.Channel1.Writer.TryComplete();
                    oldChannels.Channel2.Writer.TryComplete();
                }

                _channels = _channels.SetNext(value);
            }
        }
    }

    public RpcTestConnection(RpcTestClient testClient, RpcPeerRef clientPeerRef, RpcPeerRef serverPeerRef)
    {
        if (clientPeerRef.IsServer)
            throw new ArgumentOutOfRangeException(nameof(clientPeerRef));
        if (!serverPeerRef.IsServer)
            throw new ArgumentOutOfRangeException(nameof(serverPeerRef));

        TestClient = testClient;
        ClientPeerRef = clientPeerRef;
        ServerPeerRef = serverPeerRef;
    }

    public Task Connect(CancellationToken cancellationToken = default)
        => Connect(TestClient.Options.ConnectionFactory.Invoke(TestClient), cancellationToken);

    public async Task Connect(ChannelPair<ArrayOwner<byte>> channels, CancellationToken cancellationToken = default)
    {
        await Disconnect(cancellationToken).ConfigureAwait(false);
        var clientConnectionState = ClientPeer.ConnectionState;
        var serverConnectionState = ServerPeer.ConnectionState;
        Channels = channels;
        var connectedTask1 = clientConnectionState.WhenConnected(cancellationToken);
        var connectedTask2 = serverConnectionState.WhenConnected(cancellationToken);
        await Task.WhenAll(connectedTask1, connectedTask2).ConfigureAwait(false);
    }

    public Task Disconnect(CancellationToken cancellationToken = default)
        => Disconnect(null, cancellationToken);
    public async Task Disconnect(Exception? error, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Channels = null;
        var disconnectTask1 = ClientPeer.Disconnect(error, cancellationToken);
        var disconnectTask2 = ServerPeer.Disconnect(error, cancellationToken);
        await Task.WhenAll(disconnectTask1, disconnectTask2).ConfigureAwait(false);
    }

    public Task Reconnect(CancellationToken cancellationToken = default)
        => Reconnect(null, cancellationToken);
    public async Task Reconnect(TimeSpan? connectDelay, CancellationToken cancellationToken = default)
    {
        await Disconnect(cancellationToken).ConfigureAwait(false);
        var delay = (connectDelay ?? TimeSpan.FromMilliseconds(50)).Positive();
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        await Connect(cancellationToken).ConfigureAwait(false);
    }

    public Task Terminate(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
            _channels.TrySetFinal(RpcReconnectFailedException.DisconnectedExplicitly());
        var disconnectTask1 = ClientPeer.Disconnect(cancellationToken);
        var disconnectTask2 = ServerPeer.Disconnect(cancellationToken);
        return Task.WhenAll(disconnectTask1, disconnectTask2);
    }

    public async Task<RpcSimpleChannelTransport> PullClientTransport(RpcPeer clientPeer, CancellationToken cancellationToken)
    {
        // ReSharper disable once InconsistentlySynchronizedField
        var channels = await WhenChannelsReady(cancellationToken).ConfigureAwait(false);

        var serverTransport = new RpcSimpleChannelTransport(ServerPeer, channels.Channel2);
        var serverConnection = new RpcConnection(serverTransport);
        await ServerPeer.SetNextConnection(serverConnection, cancellationToken).ConfigureAwait(false);

        return new RpcSimpleChannelTransport(clientPeer, channels.Channel1);
    }

    // Protected methods

    protected async ValueTask<ChannelPair<ArrayOwner<byte>>> WhenChannelsReady(CancellationToken cancellationToken)
    {
        // ReSharper disable once InconsistentlySynchronizedField
        await foreach (var channels in _channels.Last.Changes(cancellationToken).ConfigureAwait(false)) {
            if (channels is null)
                continue; // Disconnected
            if (channels.Channel1.Reader.Completion.IsCompleted)
                continue; // Channel1 is closed
            if (channels.Channel2.Reader.Completion.IsCompleted)
                continue;  // Channel2 is closed
            return channels;
        }

        // Impossible to get here, but we still need to return something, so...
        throw RpcReconnectFailedException.DisconnectedExplicitly();
    }
}
