using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

#pragma warning disable CA1822

public record RpcInboundCallOptions
{
    public static RpcInboundCallOptions Default { get; set; } = new();

    // Delegate options
    public Func<RpcPeer, RpcMessage, CancellationToken, RpcInboundContext> ContextFactory { get; init; }

    public RpcInboundCallOptions()
        => ContextFactory = DefaultContextFactory;

    // Protected methods

    protected RpcInboundContext DefaultContextFactory(
        RpcPeer peer, RpcMessage message, CancellationToken peerChangedToken)
        => new(peer, message, peerChangedToken);
}
