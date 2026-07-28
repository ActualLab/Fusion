using Microsoft.Owin;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Delegate that creates an <see cref="RpcRef"/> for an OWIN WebSocket server connection
/// based on the OWIN context and backend flag.
/// </summary>
public delegate RpcRef RpcWebSocketServerRefFactory(RpcWebSocketServer server, IOwinContext context, bool isBackend);

/// <summary>
/// Delegate that creates OWIN WebSocket accept options for an incoming
/// WebSocket connection, allowing per-connection accept settings
/// based on the OWIN context and peer reference.
/// </summary>
public delegate IDictionary<string, object> RpcWebSocketServerAcceptContextFactory(
    RpcWebSocketServer server, IOwinContext context, RpcRef rpcRef);

/// <summary>
/// Delegate that decides whether a WebSocket upgrade request may be accepted, based on its
/// <c>Origin</c> header value - which is <c>""</c> when the header is absent.
/// See <see cref="RpcWebSocketServerOriginValidators"/> for ready-made implementations.
/// </summary>
public delegate bool RpcWebSocketServerOriginValidator(
    RpcWebSocketServer server, IOwinContext context, string origin);

/// <summary>
/// Provides default delegate implementations for the OWIN-based
/// <see cref="RpcWebSocketServer"/>, including the peer reference factory.
/// </summary>
public static class RpcWebSocketServerDefaultDelegates
{
    public static RpcWebSocketServerRefFactory RefFactory { get; set; } =
        static (server, context, isBackend) => {
            var query = context.Request.Query;
            var clientId = query[server.Settings.ClientIdParameterName];
            var serializationFormat = query[server.Settings.SerializationFormatParameterName];
            return RpcRef.NewServer(clientId, serializationFormat, isBackend);
        };

    public static RpcWebSocketServerAcceptContextFactory AcceptContextFactory { get; set; } =
        static (server, context, rpcRef) => {
            var acceptOptions = new Dictionary<string, object>(StringComparer.Ordinal);
            var subProtocols = context.Request.Headers.GetValues("Sec-WebSocket-Protocol");
            if (subProtocols is { Count: > 0 }) // Select the first sub-protocol offered by the client
                acceptOptions.Add("websocket.SubProtocol", subProtocols[0].Split(',').First().Trim());
            return acceptOptions;
        };

    // The default is AllowAll to keep every client that works today working: Blazor WASM served
    // from another host, dev servers on another port, mobile WebViews (capacitor://, ionic://,
    // or a literal "null"), and deliberate iframe embeds all send a foreign Origin.
    public static RpcWebSocketServerOriginValidator OriginValidator { get; set; } =
        RpcWebSocketServerOriginValidators.AllowAll;
}
