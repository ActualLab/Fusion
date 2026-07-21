using Microsoft.AspNetCore.Http;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Delegate that creates an <see cref="RpcRef"/> for an HTTP server connection
/// based on the HTTP context and backend flag.
/// </summary>
public delegate RpcRef RpcHttpServerRefFactory(RpcHttpServer server, HttpContext context, bool isBackend);

/// <summary>
/// Provides default delegate implementations for <see cref="RpcHttpServer"/>,
/// including the peer reference factory.
/// </summary>
public static class RpcHttpServerDefaultDelegates
{
    public static RpcHttpServerRefFactory RefFactory { get; set; } =
        static (server, context, isBackend) => {
            var query = context.Request.Query;
            var clientId = query[server.Options.ClientIdParameterName].SingleOrDefault() ?? "";
            var serializationFormat = query[server.Options.SerializationFormatParameterName].SingleOrDefault() ?? "";
            return RpcRef.NewServer(clientId, serializationFormat, isBackend);
        };
}
