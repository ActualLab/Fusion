using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Server.Rpc;

public class SessionBoundRpcConnection(Channel<RpcMessage> channel, ImmutableOptionSet options, Session session)
    : RpcConnection(channel, options)
{
    public Session Session { get; init; } = session;
}
