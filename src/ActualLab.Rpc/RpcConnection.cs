using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcConnection : IAsyncDisposable
{
    public RpcTransport Transport { get; }
    public PropertyBag Properties { get; init; }
    public bool IsLocal { get; init; }

    public IAsyncEnumerable<RpcInboundMessage> InboundMessages => Transport;

    public RpcConnection(RpcTransport transport, PropertyBag properties)
    {
        Transport = transport;
        Properties = properties;
    }

    public RpcConnection(RpcTransport transport)
        : this(transport, PropertyBag.Empty)
    { }

    public ValueTask DisposeAsync()
        => Transport.DisposeAsync();
}
