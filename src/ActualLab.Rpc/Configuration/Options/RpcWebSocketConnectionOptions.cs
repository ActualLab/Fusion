using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc;

public class RpcWebSocketClientOptions
{
    public static RpcWebSocketClientOptions Default { get; set; } = new();

    public bool UseAutoFrameDelayerFactory { get; init; } = false;
    public Func<FrameDelayer?>? FrameDelayerFactory { get; init; } = FrameDelayerFactories.None;

    public virtual WebSocketChannel<RpcMessage>.Options GetChannelOptions(RpcPeer peer, PropertyBag properties)
        => WebSocketChannel<RpcMessage>.Options.Default with {
            Serializer = peer.Hub.SerializationFormats.Get(peer.Ref).MessageSerializerFactory.Invoke(peer),
            FrameDelayerFactory = UseAutoFrameDelayerFactory
                ? FrameDelayerFactories.Auto(peer, properties)
                : FrameDelayerFactory,
        };
}
