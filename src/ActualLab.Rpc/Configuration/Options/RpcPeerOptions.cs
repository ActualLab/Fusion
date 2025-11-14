using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public record RpcPeerOptions
{
    public static RpcPeerOptions Default { get; set; } = new();

    // Delegate options
    public Func<RpcHub, RpcPeerRef, RpcPeer> PeerFactory { get; init; }
    public Func<RpcPeerRef, RpcPeerConnectionKind> ConnectionKindDetector { get; init; }
    public Func<RpcPeer, Exception, bool> TerminalErrorDetector { get; init; }
    public Func<RpcServerPeer, Channel<RpcMessage>, PropertyBag, CancellationToken, Task<RpcConnection>> ServerConnectionFactory { get; init; }
    public Func<RpcServerPeer, TimeSpan> ServerPeerShutdownTimeoutProvider { get; init; }

    // ReSharper disable once ConvertConstructorToMemberInitializers
    public RpcPeerOptions()
    {
        PeerFactory = DefaultPeerFactory;
        ConnectionKindDetector = DefaultConnectionKindDetector;
        TerminalErrorDetector = DefaultTerminalErrorDetector;
        ServerConnectionFactory = DefaultServerConnectionFactory;
        ServerPeerShutdownTimeoutProvider = DefaultServerPeerShutdownTimeoutProvider;
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
        RpcServerPeer peer, Channel<RpcMessage> channel, PropertyBag properties,
        CancellationToken cancellationToken)
        => Task.FromResult(new RpcConnection(channel, properties));

    protected static TimeSpan DefaultServerPeerShutdownTimeoutProvider(RpcServerPeer peer)
    {
        var peerLifetime = peer.CreatedAt.Elapsed;
        return peerLifetime.MultiplyBy(0.33).Clamp(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(15));
    }
}
