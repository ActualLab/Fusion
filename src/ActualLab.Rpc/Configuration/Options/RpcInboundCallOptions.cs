using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcInboundCallOptions(IServiceProvider services) : RpcServiceBase(services)
{
    public virtual RpcInboundContext CreateContext(
        RpcPeer peer, RpcMessage message, CancellationToken peerChangedToken)
        => new(peer, message, peerChangedToken);
}
