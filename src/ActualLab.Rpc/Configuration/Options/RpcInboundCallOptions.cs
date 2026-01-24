using ActualLab.OS;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

#pragma warning disable CA1822

public record RpcInboundCallOptions
{
    public static RpcInboundCallOptions Default { get; set; } = new();

    // Delegate options
    public Func<RpcPeer, RpcInboundMessage, CancellationToken, RpcInboundContext> ContextFactory { get; init; }

    public RpcInboundCallOptions()
        => ContextFactory = static (peer, message, peerChangedToken) => new(peer, message, peerChangedToken);
}
