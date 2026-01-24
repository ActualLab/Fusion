using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Server.Rpc;

public class SessionBoundRpcConnection(RpcTransport transport, PropertyBag properties, Session session)
    : RpcConnection(transport, properties)
{
    public Session Session { get; init; } = session;
}
