using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcConnection(Channel<RpcMessage> channel, PropertyBag properties)
{
    public Channel<RpcMessage> Channel { get; } = channel;
    public PropertyBag Properties { get; init; } = properties;
    public bool IsLocal { get; init; }

    public RpcConnection(Channel<RpcMessage> channel)
        : this(channel, PropertyBag.Empty)
    { }
}
