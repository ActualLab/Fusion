using Microsoft.Owin;

namespace ActualLab.Rpc.Server;

public delegate RpcPeerRef RpcWebSocketServerPeerRefFactory(RpcWebSocketServer server, IOwinContext context, bool isBackend);

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
