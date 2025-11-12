using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcInboundCallOptions
{
    public static RpcInboundCallOptions Default { get; set; } = new();

    public virtual RpcInboundContext CreateContext(
        RpcPeer peer, RpcMessage message, CancellationToken peerChangedToken)
        => new(peer, message, peerChangedToken);
}
