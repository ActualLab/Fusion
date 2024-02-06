using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;
using Microsoft.Owin;
using ActualLab.Internal;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.WebSockets;
using WebSocketAccept = System.Action<
    System.Collections.Generic.IDictionary<string, object>, // WebSocket Accept parameters
    System.Func< // WebSocketFunc callback
        System.Collections.Generic.IDictionary<string, object>, // WebSocket environment
        System.Threading.Tasks.Task>>;

namespace ActualLab.Rpc.Server;

public class RpcWebSocketServer(
    RpcWebSocketServer.Options settings,
    IServiceProvider services
    ) : RpcServiceBase(services)
{
    public record Options
    {
        public static Options Default { get; set; } = new();

        public bool ExposeBackend { get; init; } = false;
        public string RequestPath { get; init; } = RpcWebSocketClient.Options.Default.RequestPath;
        public string BackendRequestPath { get; init; } = RpcWebSocketClient.Options.Default.BackendRequestPath;
        public string ClientIdParameterName { get; init; } = RpcWebSocketClient.Options.Default.ClientIdParameterName;
        public WebSocketChannel<RpcMessage>.Options WebSocketChannelOptions { get; init; } = WebSocketChannel<RpcMessage>.Options.Default;
    }

    public Options Settings { get; } = settings;
    public RpcWebSocketServerPeerRefFactory PeerRefFactory { get; }
        = services.GetRequiredService<RpcWebSocketServerPeerRefFactory>();
    public RpcServerConnectionFactory ServerConnectionFactory { get; }
        = services.GetRequiredService<RpcServerConnectionFactory>();

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public HttpStatusCode Invoke(IOwinContext context, bool isBackend)
    {
        // Based on https://stackoverflow.com/questions/41848095/websockets-using-owin

        var acceptToken = context.Get<WebSocketAccept>("websocket.Accept");
        if (acceptToken == null)
            return HttpStatusCode.BadRequest;

        var peerRef = PeerRefFactory.Invoke(this, context, isBackend).RequireServer();
        _ = Hub.GetServerPeer(peerRef);

        var requestHeaders =
            GetValue<IDictionary<string, string[]>>(context.Environment, "owin.RequestHeaders")
            ?? ImmutableDictionary<string, string[]>.Empty;

        var acceptOptions = new Dictionary<string, object>(StringComparer.Ordinal);
        if (requestHeaders.TryGetValue("Sec-WebSocket-Protocol", out string[]? subProtocols) && subProtocols.Length > 0) {
            // Select the first one from the client
            acceptOptions.Add("websocket.SubProtocol", subProtocols[0].Split(',').First().Trim());
        }

        acceptToken(acceptOptions, wsEnv => {
            var wsContext = (WebSocketContext)wsEnv["System.Net.WebSockets.WebSocketContext"];
            return HandleWebSocket(context, wsContext, isBackend);
        });

        return HttpStatusCode.SwitchingProtocols;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private async Task HandleWebSocket(IOwinContext context, WebSocketContext wsContext, bool isBackend)
    {
        var peerRef = PeerRefFactory.Invoke(this, context, isBackend);
        var peer = Hub.GetServerPeer(peerRef);
        var cancellationToken = context.Request.CallCancelled;
        try {
            var webSocket = wsContext.WebSocket;
            var webSocketOwner = new WebSocketOwner(peer.Ref.Key, webSocket, Services);
            var channel = new WebSocketChannel<RpcMessage>(
                Settings.WebSocketChannelOptions, webSocketOwner, cancellationToken) {
                OwnsWebSocketOwner = false,
            };
            var options = ImmutableOptionSet.Empty.Set(context).Set(webSocket);
            var connection = await ServerConnectionFactory
                .Invoke(peer, channel, options, cancellationToken)
                .ConfigureAwait(false);

            peer.SetConnection(connection);
            await channel.WhenClosed.ConfigureAwait(false);
        }
        catch (Exception e) when (e.IsCancellationOf(cancellationToken)) {
            // Intended: this is typically a normal connection termination
        }
    }

    private static T? GetValue<T>(IDictionary<string, object?> env, string key)
        => env.TryGetValue(key, out var value) && value is T result ? result : default;
}
