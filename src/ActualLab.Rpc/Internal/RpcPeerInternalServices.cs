using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Internal;

/// <summary>
/// Provides convenient access to commonly used services and options associated with an <see cref="RpcPeer"/>.
/// </summary>
public readonly record struct RpcPeerInternalServices(RpcPeer Peer)
{
    public IServiceProvider Services => Peer.Services;
    public RpcPeerOptions Options => Peer.Options;
    public RpcOutboundCallOptions OutboundCallOptions => Peer.OutboundCallOptions;
    public RpcInboundCallOptions InboundCallOptions => Peer.InboundCallOptions;
    public RpcTransport? Transport => Peer.Transport;
    public RpcCallLogger CallLogger => Peer.CallLogger;
    public ILogger Log => Peer.Log;
}
