using Microsoft.Owin;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Delegate that creates an <see cref="RpcPeerRef"/> for an OWIN WebSocket server connection
/// based on the OWIN context and backend flag.
/// </summary>
public delegate RpcPeerRef RpcWebSocketServerPeerRefFactory(RpcWebSocketServer server, IOwinContext context, bool isBackend);

/// <summary>
/// Provides default delegate implementations for the OWIN-based
/// <see cref="RpcWebSocketServer"/>, including the peer reference factory.
/// </summary>
public static class RpcWebSocketServerDefaultDelegates
{
    public static RpcWebSocketServerPeerRefFactory PeerRefFactory { get; set; } =
        static (server, context, isBackend) => {
            var query = context.Request.Query;
            var clientId = query[server.Settings.ClientIdParameterName];
            var serializationFormat = query[server.Settings.SerializationFormatParameterName];
            return RpcPeerRef.NewServer(clientId, serializationFormat, isBackend);
        };
}
