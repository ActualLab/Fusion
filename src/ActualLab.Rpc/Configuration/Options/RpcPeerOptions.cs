using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

/// <summary>
/// Configuration options controlling RPC peer creation, connection management, and shutdown behavior.
/// </summary>
public record RpcPeerOptions
{
    public static RpcPeerOptions Default { get; set; } = new();

    // Random handshake index reliably triggers Reconnect failures if there is any issue w/ handshake index check.
    // - false = compatible w/ pre-v11.4 clients containing a bug w/ handshake index check
    // - true = good for testing.
    public bool UseRandomHandshakeIndex { get; init; } = false;

    // Delegate options
    public Func<RpcHub, RpcRoute, RpcPeer> PeerFactory { get; init; }
    public Func<RpcRoute, RpcPeerConnectionKind> ConnectionKindDetector { get; init; }
    public Func<RpcPeer, Exception, bool> TerminalErrorDetector { get; init; }
    public Func<RpcServerPeer, RpcTransport, PropertyBag, CancellationToken, Task<RpcConnection>> ServerConnectionFactory { get; init; }
    public Func<RpcServerPeer, TimeSpan> ServerPeerShutdownTimeoutProvider { get; init; }
    public Func<RpcPeer, TimeSpan> PeerRemoveDelayProvider { get; init; }

    // ReSharper disable once ConvertConstructorToMemberInitializers
    public RpcPeerOptions()
    {
        PeerFactory = DefaultPeerFactory;
        ConnectionKindDetector = DefaultConnectionKindDetector;
        TerminalErrorDetector = DefaultTerminalErrorDetector;
        ServerConnectionFactory = DefaultServerConnectionFactory;
        ServerPeerShutdownTimeoutProvider = DefaultServerPeerShutdownTimeoutProvider;
        PeerRemoveDelayProvider = DefaultPeerRemoveDelayProvider;
    }

    // Protected methods

    protected static RpcPeer DefaultPeerFactory(RpcHub hub, RpcRoute route)
        => route.Ref.IsServer
            ? new RpcServerPeer(hub, route)
            : new RpcClientPeer(hub, route);

    protected static RpcPeerConnectionKind DefaultConnectionKindDetector(RpcRoute route)
        => route.Ref.ConnectionKind;

    protected static bool DefaultTerminalErrorDetector(RpcPeer peer, Exception error)
        => error is RpcReconnectFailedException or RpcSerializationFormatException;

    protected static Task<RpcConnection> DefaultServerConnectionFactory(
        RpcServerPeer peer, RpcTransport transport, PropertyBag properties,
        CancellationToken cancellationToken)
        => Task.FromResult(new RpcConnection(transport, properties));

    protected static TimeSpan DefaultServerPeerShutdownTimeoutProvider(RpcServerPeer peer)
    {
        var peerLifetime = Moment.Now - peer.CreatedAt;
        return peerLifetime.MultiplyBy(0.33).Clamp(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(15));
    }

    protected static TimeSpan DefaultPeerRemoveDelayProvider(RpcPeer peer)
    {
        if (peer.Route.IsChanged)
            // The peer is already replaced by the next route generation's one
            return TimeSpan.Zero;

        return peer.Ref.IsServer
            // Server peers can be safely recreated on reconnection later, so it's safe to remove them instantly
            ? TimeSpan.Zero
            // Client peer's termination is final. We remove them eventually only to prevent memory leaks.
            : TimeSpan.FromMinutes(5);
    }
}
