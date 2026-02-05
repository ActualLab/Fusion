using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Server.Rpc;

/// <summary>
/// An <see cref="RpcConnection"/> that carries an associated <see cref="Session"/>,
/// enabling server-side session resolution for inbound RPC calls.
/// </summary>
public class SessionBoundRpcConnection(RpcTransport transport, PropertyBag properties, Session session)
    : RpcConnection(transport, properties)
{
    public Session Session { get; init; } = session;
}
