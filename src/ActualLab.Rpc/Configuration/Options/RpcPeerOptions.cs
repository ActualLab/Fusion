using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public record RpcPeerOptions
{
    public static RpcPeerOptions Default { get; set; } = new();

    // Random handshake index reliably triggers Reconnect failures if there is any issue w/ handshake index check.
    // - false = compatible w/ pre-v11.4 clients containing a bug w/ handshake index check
    // - true = good for testing.
    public bool UseRandomHandshakeIndex { get; init; } = false;

    // Delegate options
    public Func<RpcHub, RpcPeerRef, RpcPeer> PeerFactory { get; init; }
    public Func<RpcPeerRef, RpcPeerConnectionKind> ConnectionKindDetector { get; init; }
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

    protected static RpcPeer DefaultPeerFactory(RpcHub hub, RpcPeerRef peerRef)
        => peerRef.IsServer
            ? new RpcServerPeer(hub, peerRef)
            : new RpcClientPeer(hub, peerRef);

    protected static RpcPeerConnectionKind DefaultConnectionKindDetector(RpcPeerRef peerRef)
        => peerRef.ConnectionKind;

    protected static bool DefaultTerminalErrorDetector(RpcPeer peer, Exception error)
        => error is RpcReconnectFailedException;

    protected static Task<RpcConnection> DefaultServerConnectionFactory(
        RpcServerPeer peer, RpcTransport transport, PropertyBag properties,
        CancellationToken cancellationToken)
        => Task.FromResult(new RpcConnection(transport, properties));

    protected static TimeSpan DefaultServerPeerShutdownTimeoutProvider(RpcServerPeer peer)
    {
        var peerLifetime = peer.CreatedAt.Elapsed;
        return peerLifetime.MultiplyBy(0.33).Clamp(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(15));
    }

    protected static TimeSpan DefaultPeerRemoveDelayProvider(RpcPeer peer)
        => peer.Ref.IsServer
            // Server peers can be safely recreated on reconnection later, so it's safe to remove them instantly
            ? TimeSpan.Zero
            // Client peer's termination is final. We remove them eventually only to prevent memory leaks.
            : TimeSpan.FromMinutes(5);
}
