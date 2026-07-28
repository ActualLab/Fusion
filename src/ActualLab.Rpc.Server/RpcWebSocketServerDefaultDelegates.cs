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
/// Delegate that decides whether a WebSocket upgrade request may be accepted, based on its
/// <c>Origin</c> header value - which is <c>""</c> when the header is absent.
/// See <see cref="RpcWebSocketServerOriginValidators"/> for ready-made implementations.
/// </summary>
public delegate bool RpcWebSocketServerOriginValidator(
    RpcWebSocketServer server, HttpContext context, string origin);

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

    // The default is AllowAll to keep every client that works today working: Blazor WASM served
    // from another host, dev servers on another port, mobile WebViews (capacitor://, ionic://,
    // or a literal "null"), and deliberate iframe embeds all send a foreign Origin.
    public static RpcWebSocketServerOriginValidator OriginValidator { get; set; } =
        RpcWebSocketServerOriginValidators.AllowAll;
}
