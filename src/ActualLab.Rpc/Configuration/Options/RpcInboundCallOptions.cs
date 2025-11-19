using ActualLab.OS;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

#pragma warning disable CA1822

public record RpcInboundCallOptions
{
    public static RpcInboundCallOptions Default { get; set; } = new();

    public bool UseNullabilityArgumentValidator { get; init; } = RuntimeInfo.IsServer;
    // Delegate options
    public Func<RpcPeer, RpcMessage, CancellationToken, RpcInboundContext> ContextFactory { get; init; }
    public Func<RpcMethodDef, Func<RpcInboundCall, Task>, Func<RpcInboundCall, Task>>? InboundCallServerInvokerDecorator { get; init; }

    public RpcInboundCallOptions()
    {
        ContextFactory = static (peer, message, peerChangedToken) => new(peer, message, peerChangedToken);
        InboundCallServerInvokerDecorator = null;
    }
}
