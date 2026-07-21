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

/// <summary>
/// Server-side handler that accepts incoming WebSocket connections and establishes
/// RPC peer connections for OWIN-based .NET Framework hosts.
/// </summary>
public class RpcWebSocketServer(RpcWebSocketServerOptions settings, IServiceProvider services)
    : RpcServiceBase(services)
{
    public RpcWebSocketServerOptions Settings { get; } = settings;
    public RpcPeerOptions PeerOptions { get; } = services.GetRequiredService<RpcPeerOptions>();
    public RpcWebSocketClientOptions WebSocketClientOptions { get; } = services.GetRequiredService<RpcWebSocketClientOptions>();
    public RpcWebSocketServerRefFactory RefFactory { get; } = services.GetRequiredService<RpcWebSocketServerRefFactory>();

    public virtual async Task<HttpStatusCode> Invoke(IOwinContext context, bool isBackend)
    {
        // Based on https://stackoverflow.com/questions/41848095/websockets-using-owin

        var acceptToken = context.Get<WebSocketAccept>("websocket.Accept");
        if (acceptToken is null)
            return HttpStatusCode.BadRequest;

        var rpcRef = RefFactory.Invoke(this, context, isBackend).RequireServer();

        // Validate serialization format before peer creation to avoid KeyNotFoundException.
        // Empty format is also rejected — clients must specify one explicitly.
        if (!Hub.SerializationFormats.TryGet(rpcRef.SerializationFormat, out _)) {
            Log.LogWarning("'{PeerRef}': Unsupported RPC serialization format '{Format}'",
                rpcRef, rpcRef.SerializationFormat);
            return HttpStatusCode.BadRequest;
        }

        var peer = Hub.GetServerPeer(rpcRef);

        // Disconnect any stale connection BEFORE upgrading the new WebSocket.
        // Doing this after the upgrade would consume the client's HandshakeTimeout,
        // because old-connection teardown can take up to RpcWebSocketTransport.Options.CloseTimeout
        // on a dead socket; performing it before the upgrade consumes ConnectTimeout instead,
        // which is the correct budget for "waiting for server to be ready to talk".
        // Use IsConnectedOrHandshaking, not IsConnected: a client reconnecting faster than
        // its previous handshake completes would otherwise stack new connections against a
        // peer stuck mid-handshake instead of replacing the stale one.
        if (peer.ConnectionState.Value.IsConnectingOrConnected()) {
            Log.LogWarning("'{PeerRef}': {Peer} is already connected, disconnecting the old connection first...",
                rpcRef, peer);
            try {
                await peer.Disconnect(context.Request.CallCancelled).ConfigureAwait(false);
            }
            catch (Exception e) when (!e.IsCancellationOf(context.Request.CallCancelled)) {
                Log.LogWarning(e, "'{PeerRef}': Failed to disconnect old connection", rpcRef);
                return HttpStatusCode.InternalServerError;
            }
        }

        var acceptOptions = Settings.ConfigureWebSocket.Invoke(this, context, rpcRef);
        acceptToken(acceptOptions, wsEnv => {
            var wsContext = (WebSocketContext)wsEnv["System.Net.WebSockets.WebSocketContext"];
            return HandleWebSocket(context, wsContext, rpcRef);
        });

        return HttpStatusCode.SwitchingProtocols;
    }

    private async Task HandleWebSocket(IOwinContext context, WebSocketContext wsContext, RpcRef rpcRef)
    {
        var cancellationToken = context.Request.CallCancelled;
        WebSocket? webSocket = null;
        WebSocketOwner? webSocketOwner = null;
        RpcConnection? connection = null;
        try {
            var peer = Hub.GetServerPeer(rpcRef);

            webSocket = wsContext.WebSocket;
            var properties = PropertyBag.Empty
                .KeylessSet((RpcPeer)peer)
                .KeylessSet(context)
                .KeylessSet(webSocket);
            webSocketOwner = new WebSocketOwner(peer.Route.ToString(), webSocket, Services);
            var transportOptions = WebSocketClientOptions.WebSocketTransportOptionsFactory.Invoke(peer, properties);
            var stopTokenSource = cancellationToken.CreateLinkedTokenSource();
            var transport = new RpcWebSocketTransport(transportOptions, peer, webSocketOwner, stopTokenSource) {
                OwnsWebSocketOwner = false,
            };
            connection = await PeerOptions.ServerConnectionFactory
                .Invoke(peer, transport, properties, cancellationToken)
                .ConfigureAwait(false);

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
        finally {
            if (webSocketOwner is not null)
                await webSocketOwner.DisposeAsync().ConfigureAwait(false);
            else
                webSocket?.Dispose();
        }
    }
}
