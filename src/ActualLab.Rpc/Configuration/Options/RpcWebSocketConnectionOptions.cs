using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc;

public class RpcWebSocketClientOptions(IServiceProvider services) : RpcServiceBase(services)
{
    public virtual WebSocketChannel<RpcMessage>.Options GetChannelOptions(RpcPeer peer, PropertyBag properties)
        => WebSocketChannel<RpcMessage>.Options.Default with {
            Serializer = peer.Hub.SerializationFormats.Get(peer.Ref).MessageSerializerFactory.Invoke(peer),
        };
}
