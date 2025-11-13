using Microsoft.AspNetCore.Http;
using ActualLab.Fusion.Server.Middlewares;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Server.Rpc;

public static class RpcOptionsForFusionServer
{
    public const string SessionParameterName = "session";

    public static RpcPeerOptions DefaultPeerOptions { get; set; }
        = new() { ServerConnectionFactory = FusionServerConnectionFactory };

    // Private methods

    private static Task<RpcConnection> FusionServerConnectionFactory(
        RpcServerPeer peer, Channel<RpcMessage> channel, PropertyBag properties,
        CancellationToken cancellationToken)
    {
        if (!properties.KeylessTryGet<HttpContext>(out var httpContext))
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

    private static Task<RpcConnection> CreateSessionBoundRpcConnectionAsync(
        Channel<RpcMessage> channel, PropertyBag properties, Session session)
        => Task.FromResult<RpcConnection>(new SessionBoundRpcConnection(channel, properties, session));

    private static Task<RpcConnection> CreateRpcConnectionAsync(
        Channel<RpcMessage> channel, PropertyBag properties)
        => Task.FromResult(new RpcConnection(channel, properties));
}
