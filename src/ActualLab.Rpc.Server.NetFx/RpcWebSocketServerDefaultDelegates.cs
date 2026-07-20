using Microsoft.Owin;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Delegate that creates an <see cref="RpcPeerRef"/> for an OWIN WebSocket server connection
/// based on the OWIN context and backend flag.
/// </summary>
public delegate RpcPeerRef RpcWebSocketServerPeerRefFactory(RpcWebSocketServer server, IOwinContext context, bool isBackend);

/// <summary>
/// Delegate that creates OWIN WebSocket accept options for an incoming
/// WebSocket connection, allowing per-connection accept settings
/// based on the OWIN context and peer reference.
/// </summary>
public delegate IDictionary<string, object> RpcWebSocketServerAcceptContextFactory(
    RpcWebSocketServer server, IOwinContext context, RpcPeerRef peerRef);

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

    public static RpcWebSocketServerAcceptContextFactory AcceptContextFactory { get; set; } =
        static (server, context, peerRef) => {
            var acceptOptions = new Dictionary<string, object>(StringComparer.Ordinal);
            var subProtocols = context.Request.Headers.GetValues("Sec-WebSocket-Protocol");
            if (subProtocols is { Count: > 0 }) // Select the first sub-protocol offered by the client
                acceptOptions.Add("websocket.SubProtocol", subProtocols[0].Split(',').First().Trim());
            return acceptOptions;
        };
}
