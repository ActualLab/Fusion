using ActualLab.Fusion.Server.Middlewares;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace ActualLab.Fusion.Server.Rpc;

/// <summary>
/// Extension methods for <see cref="RpcPeerOptions"/> to apply Fusion server overrides,
/// including session-bound connection factory support.
/// </summary>
public static class RpcPeerOptionsExt
{
    public const string SessionParameterName = "session";

    public static RpcPeerOptions WithFusionServerOverrides(this RpcPeerOptions options)
        => options with { ServerConnectionFactory = ServerConnectionFactory };

    // Private methods

    private static Task<RpcConnection> ServerConnectionFactory(
        RpcServerPeer peer, RpcTransport transport, PropertyBag properties,
        CancellationToken cancellationToken)
    {
        if (!properties.KeylessTryGet<HttpContext>(out var httpContext))
            return CreateRpcConnectionAsync(transport, properties);

        var query = httpContext.Request.Query;
        var sessionId = query[SessionParameterName].SingleOrDefault() ?? "";
        if (!sessionId.IsNullOrEmpty() && new Session(sessionId) is var session1 && session1.IsValid())
            return CreateSessionBoundRpcConnectionAsync(transport, properties, session1);

        var sessionMiddleware = httpContext.RequestServices.GetService<SessionMiddleware>();
        if (sessionMiddleware?.GetSession(httpContext) is { } session2 && session2.IsValid())
            return CreateSessionBoundRpcConnectionAsync(transport, properties, session2);

        return CreateRpcConnectionAsync(transport, properties);
    }

    private static Task<RpcConnection> CreateSessionBoundRpcConnectionAsync(
        RpcTransport transport, PropertyBag properties, Session session)
        => Task.FromResult<RpcConnection>(new SessionBoundRpcConnection(transport, properties, session));

    private static Task<RpcConnection> CreateRpcConnectionAsync(
        RpcTransport transport, PropertyBag properties)
        => Task.FromResult(new RpcConnection(transport, properties));

}
