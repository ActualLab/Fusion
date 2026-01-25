using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcConnection(RpcTransport transport, PropertyBag properties) : IAsyncDisposable
{
    public RpcTransport Transport { get; } = transport;
    public PropertyBag Properties { get; init; } = properties;
    public bool IsLocal { get; init; }

    public IAsyncEnumerable<RpcInboundMessage> InboundMessages => Transport;

    public RpcConnection(RpcTransport transport)
        : this(transport, PropertyBag.Empty)
    { }

    public ValueTask DisposeAsync()
        => Transport.DisposeAsync();
}
