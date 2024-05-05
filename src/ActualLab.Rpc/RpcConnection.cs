using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcConnection(Channel<RpcMessage> channel, PropertyBag properties)
{
    public Channel<RpcMessage> Channel { get; } = channel;
    public PropertyBag Properties { get; set; } = properties;

    public RpcConnection(Channel<RpcMessage> channel)
        : this(channel, PropertyBag.Empty)
    { }
}
