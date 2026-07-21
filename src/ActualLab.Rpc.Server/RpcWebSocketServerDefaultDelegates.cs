using Microsoft.AspNetCore.Http;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Delegate that creates an <see cref="RpcRef"/> for a WebSocket server connection
/// based on the HTTP context and backend flag.
/// </summary>
public delegate RpcRef RpcWebSocketServerRefFactory(RpcWebSocketServer server, HttpContext context, bool isBackend);

#if NET6_0_OR_GREATER
/// <summary>
/// Delegate that creates a <see cref="WebSocketAcceptContext"/> for an incoming
/// WebSocket connection, allowing per-connection accept settings
/// (e.g. compression) based on the HTTP context and peer reference.
/// </summary>
public delegate WebSocketAcceptContext RpcWebSocketServerAcceptContextFactory(
    RpcWebSocketServer server, HttpContext context, RpcRef rpcRef);
#endif

/// <summary>
/// Provides default delegate implementations for <see cref="RpcWebSocketServer"/>,
/// including the peer reference factory.
/// </summary>
public static class RpcWebSocketServerDefaultDelegates
{
    public static RpcWebSocketServerRefFactory RefFactory { get; set; } =
        static (server, context, isBackend) => {
            var query = context.Request.Query;
            var clientId = query[server.Options.ClientIdParameterName].SingleOrDefault() ?? "";
            var serializationFormat = query[server.Options.SerializationFormatParameterName].SingleOrDefault() ?? "";
            return RpcRef.NewServer(clientId, serializationFormat, isBackend);
        };

#if NET6_0_OR_GREATER
    public static RpcWebSocketServerAcceptContextFactory AcceptContextFactory { get; set; } =
        static (server, context, rpcRef) => new();
#endif
}
