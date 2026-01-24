using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Internal;

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
