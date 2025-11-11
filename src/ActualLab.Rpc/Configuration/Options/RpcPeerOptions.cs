using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcPeerOptions(IServiceProvider services) : RpcServiceBase(services)
{
    public virtual RpcPeer CreatePeer(RpcPeerRef peerRef)
        => peerRef.IsServer
            ? new RpcServerPeer(Hub, peerRef)
            : new RpcClientPeer(Hub, peerRef);

    public virtual RpcPeerConnectionKind GetConnectionKind(RpcPeerRef peerRef)
        => peerRef.ConnectionKind;

    public virtual bool IsTerminalError(Exception error)
        => error is RpcReconnectFailedException;

    // Server peer related

    public virtual Task<RpcConnection> CreateServerConnection(
        RpcServerPeer peer, Channel<RpcMessage> channel, PropertyBag properties,
        CancellationToken cancellationToken)
        => Task.FromResult(new RpcConnection(channel, properties));

    public virtual TimeSpan GetServerPeerCloseTimeout(RpcServerPeer peer)
    {
        var peerLifetime = peer.CreatedAt.Elapsed;
        return peerLifetime.MultiplyBy(0.33).Clamp(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(15));
    }
}
