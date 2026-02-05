using Microsoft.AspNetCore.Http;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Delegate that creates an <see cref="RpcPeerRef"/> for a WebSocket server connection
/// based on the HTTP context and backend flag.
/// </summary>
public delegate RpcPeerRef RpcWebSocketServerPeerRefFactory(RpcWebSocketServer server, HttpContext context, bool isBackend);

/// <summary>
/// Provides default delegate implementations for <see cref="RpcWebSocketServer"/>,
/// including the peer reference factory.
/// </summary>
public static class RpcWebSocketServerDefaultDelegates
{
    public static RpcWebSocketServerPeerRefFactory PeerRefFactory { get; set; } =
        static (server, context, isBackend) => {
            var query = context.Request.Query;
            var clientId = query[server.Options.ClientIdParameterName].SingleOrDefault() ?? "";
            var serializationFormat = query[server.Options.SerializationFormatParameterName].SingleOrDefault() ?? "";
            return RpcPeerRef.NewServer(clientId, serializationFormat, isBackend);
        };
}
