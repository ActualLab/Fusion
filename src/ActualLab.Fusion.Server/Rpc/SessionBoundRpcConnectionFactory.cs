using Microsoft.AspNetCore.Http;
using ActualLab.Fusion.Server.Middlewares;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Server.Rpc;

public class SessionBoundRpcConnectionFactory
{
    public string SessionParameterName { get; init; } = "session";

    public Task<RpcConnection> Invoke(
        RpcServerPeer peer, Channel<RpcMessage> channel, PropertyBag properties,
        CancellationToken cancellationToken)
    {
        if (!properties.TryGetKeyless<HttpContext>(out var httpContext))
            return CreateRpcConnectionAsync(channel, properties);

        var query = httpContext.Request.Query;
        var sessionId = query[SessionParameterName].SingleOrDefault() ?? "";
        if (!sessionId.IsNullOrEmpty() && new Session(sessionId) is var session1 && session1.IsValid())
            return CreateSessionBoundRpcConnectionAsync(channel, properties, session1);

        var sessionMiddleware = httpContext.RequestServices.GetService<SessionMiddleware>();
        if (sessionMiddleware?.GetSession(httpContext) is { } session2 && session2.IsValid())
            return CreateSessionBoundRpcConnectionAsync(channel, properties, session2);

        return CreateRpcConnectionAsync(channel, properties);
    }

    protected static Task<RpcConnection> CreateSessionBoundRpcConnectionAsync(
        Channel<RpcMessage> channel, PropertyBag properties, Session session)
        => Task.FromResult<RpcConnection>(new SessionBoundRpcConnection(channel, properties, session));

    protected static Task<RpcConnection> CreateRpcConnectionAsync(
        Channel<RpcMessage> channel, PropertyBag properties)
        => Task.FromResult(new RpcConnection(channel, properties));
}
