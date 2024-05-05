using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Server.Rpc;

public class SessionBoundRpcConnection(Channel<RpcMessage> channel, PropertyBag properties, Session session)
    : RpcConnection(channel, properties)
{
    public Session Session { get; init; } = session;
}
