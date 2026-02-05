using System.Net;
using System.Net.WebSockets;
using Microsoft.Owin;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.WebSockets;
using WebSocketAccept = System.Action<
    System.Collections.Generic.IDictionary<string, object>, // WebSocket Accept parameters
    System.Func< // WebSocketFunc callback
        System.Collections.Generic.IDictionary<string, object>, // WebSocket environment
        System.Threading.Tasks.Task>>;

namespace ActualLab.Rpc.Server;

public class RpcWebSocketServer(RpcWebSocketServerOptions settings, IServiceProvider services)
    : RpcServiceBase(services)
{
    public RpcWebSocketServerOptions Settings { get; } = settings;
    public RpcPeerOptions PeerOptions { get; } = services.GetRequiredService<RpcPeerOptions>();
    public RpcWebSocketClientOptions WebSocketClientOptions { get; } = services.GetRequiredService<RpcWebSocketClientOptions>();
    public RpcWebSocketServerPeerRefFactory PeerRefFactory { get; } = services.GetRequiredService<RpcWebSocketServerPeerRefFactory>();

    public virtual HttpStatusCode Invoke(IOwinContext context, bool isBackend)
    {
        // Based on https://stackoverflow.com/questions/41848095/websockets-using-owin

        var acceptToken = context.Get<WebSocketAccept>("websocket.Accept");
        if (acceptToken is null)
            return HttpStatusCode.BadRequest;

        var peerRef = PeerRefFactory.Invoke(this, context, isBackend).RequireServer();
        _ = Hub.GetServerPeer(peerRef);

        var headers =
            GetValue<IDictionary<string, string[]>>(context.Environment, "owin.RequestHeaders")
            ?? ImmutableDictionary<string, string[]>.Empty;

        var acceptOptions = new Dictionary<string, object>(StringComparer.Ordinal);
        if (headers.TryGetValue("Sec-WebSocket-Protocol", out string[]? subProtocols) && subProtocols.Length > 0) {
            // Select the first one from the client
            acceptOptions.Add("websocket.SubProtocol", subProtocols[0].Split(',').First().Trim());
        }

        acceptToken(acceptOptions, wsEnv => {
            var wsContext = (WebSocketContext)wsEnv["System.Net.WebSockets.WebSocketContext"];
            return HandleWebSocket(context, wsContext, isBackend);
        });

        return HttpStatusCode.SwitchingProtocols;
    }

    private async Task HandleWebSocket(IOwinContext context, WebSocketContext wsContext, bool isBackend)
    {
        var cancellationToken = context.Request.CallCancelled;
        WebSocket? webSocket = null;
        RpcConnection? connection = null;
        try {
            var peerRef = PeerRefFactory.Invoke(this, context, isBackend);
            var peer = Hub.GetServerPeer(peerRef);

            webSocket = wsContext.WebSocket;
            var properties = PropertyBag.Empty
                .KeylessSet((RpcPeer)peer)
                .KeylessSet(context)
                .KeylessSet(webSocket);
            var webSocketOwner = new WebSocketOwner(peer.Ref.ToString(), webSocket, Services);
            var transportOptions = WebSocketClientOptions.WebSocketTransportOptionsFactory.Invoke(peer, properties);
            var stopTokenSource = cancellationToken.CreateLinkedTokenSource();
            var transport = new RpcWebSocketTransport(transportOptions, peer, webSocketOwner, stopTokenSource) {
                OwnsWebSocketOwner = false,
            };
            connection = await PeerOptions.ServerConnectionFactory
                .Invoke(peer, transport, properties, cancellationToken)
                .ConfigureAwait(false);

            if (peer.IsConnected()) {
                var delay = Settings.ChangeConnectionDelay;
                Log.LogWarning("{Peer} is already connected, will change its connection in {Delay}...",
                    peer, delay.ToShortString());
                await peer.Hub.SystemClock.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            await peer.SetNextConnection(connection, cancellationToken).ConfigureAwait(false);
            await transport.WhenClosed.ConfigureAwait(false);
        }
        catch (Exception e) {
            if (connection is not null || e.IsCancellationOf(cancellationToken))
                return; // Intended: this is typically a normal connection termination

            var request = context.Request;
            Log.LogWarning(e, "Failed to accept RPC connection: {Path}{Query}", request.Path, request.QueryString);
            if (webSocket is not null)
                return;

            try {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            catch {
                // Intended
            }
        }
    }

    private static T? GetValue<T>(IDictionary<string, object?> env, string key)
        => env.TryGetValue(key, out var value) && value is T result ? result : default;
}
