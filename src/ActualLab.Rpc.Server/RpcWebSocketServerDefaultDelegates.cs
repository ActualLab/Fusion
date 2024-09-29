using Microsoft.AspNetCore.Http;

namespace ActualLab.Rpc.Server;

public delegate RpcPeerRef RpcWebSocketServerPeerRefFactory(RpcWebSocketServer server, HttpContext context, bool isBackend);

public static class RpcWebSocketServerDefaultDelegates
{
    public static RpcWebSocketServerPeerRefFactory PeerRefFactory { get; set; } =
        static (server, context, isBackend) => {
            var query = context.Request.Query;
            var clientId = query[server.Settings.ClientIdParameterName].SingleOrDefault() ?? "";
            var serializationFormat = query[server.Settings.SerializationFormatParameterName].SingleOrDefault() ?? "";
            return RpcPeerRef.NewServer(clientId, serializationFormat, isBackend);
        };
}
