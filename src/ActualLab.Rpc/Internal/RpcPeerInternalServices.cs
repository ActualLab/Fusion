using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Internal;

public readonly record struct RpcPeerInternalServices(RpcPeer Peer)
{
    public ILogger Log => Peer.Log;
    public ILogger? CallLog => Peer.CallLog;
    public ChannelWriter<RpcMessage>? Sender => Peer.Sender;
}
