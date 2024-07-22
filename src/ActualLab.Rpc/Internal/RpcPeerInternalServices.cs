using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Internal;

public readonly record struct RpcPeerInternalServices(RpcPeer Peer)
{
    public ILogger Log => Peer.Log;
    public RpcCallLogger CallLogger => Peer.CallLogger;
    public ChannelWriter<RpcMessage>? Sender => Peer.Sender;
}
