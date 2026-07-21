using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Server-side handler that accepts incoming WebSocket connections and establishes
/// RPC peer connections for ASP.NET Core hosts.
/// </summary>
public class RpcWebSocketServer(RpcWebSocketServerOptions options, IServiceProvider services)
    : RpcServiceBase(services)
{
    public RpcWebSocketServerOptions Options { get; } = options;
    public RpcPeerOptions PeerOptions { get; } = services.GetRequiredService<RpcPeerOptions>();
    public RpcWebSocketClientOptions WebSocketClientOptions { get; } = services.GetRequiredService<RpcWebSocketClientOptions>();
    public RpcWebSocketServerRefFactory RefFactory { get; } = services.GetRequiredService<RpcWebSocketServerRefFactory>();

    public virtual async Task Invoke(HttpContext context, bool isBackend)
    {
        var request = context.Request;
        var uri = new UriBuilder(
            request.Scheme,
            request.Host.Host,
            request.Host.Port ?? -1,
            request.Path,
            request.QueryString.ToString());
        var requestDescription = $"{request.Method} {uri}";
        var cancellationToken = context.RequestAborted;
        if (!context.WebSockets.IsWebSocketRequest) {
            Log.LogWarning("WebSocket request expected, but got {Request}", requestDescription);
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        WebSocket? webSocket = null;
        RpcConnection? connection = null;
        RpcRef? rpcRef = null;
        try {
            rpcRef = RefFactory.Invoke(this, context, isBackend).RequireServer();
            if (!Hub.SerializationFormats.TryGet(rpcRef.SerializationFormat, out _)) {
                Log.LogWarning("'{PeerRef}': Unsupported RPC serialization format '{Format}' for {Request}",
                    rpcRef, rpcRef.SerializationFormat, requestDescription);
#if NET6_0_OR_GREATER
                var rejectAcceptContext = Options.ConfigureWebSocket.Invoke(this, context, rpcRef);
                webSocket = await context.WebSockets.AcceptWebSocketAsync(rejectAcceptContext).ConfigureAwait(false);
#else
                webSocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
#endif
                await webSocket.CloseAsync(
                    (WebSocketCloseStatus)RpcWebSocketCloseCode.UnsupportedFormat,
                    $"Unsupported RPC serialization format: '{rpcRef.SerializationFormat}'",
                    cancellationToken
                    ).ConfigureAwait(false);
                return;
            }

            Log.LogInformation("'{PeerRef}': Accepting RPC connection for {Request}", rpcRef, requestDescription);
            var peer = Hub.GetServerPeer(rpcRef);

            // Disconnect any stale connection BEFORE upgrading the new WebSocket.
            // Doing this after AcceptWebSocketAsync would consume the client's HandshakeTimeout,
            // because old-connection teardown can take up to RpcWebSocketTransport.Options.CloseTimeout
            // on a dead socket; performing it before the upgrade consumes ConnectTimeout instead,
            // which is the correct budget for "waiting for server to be ready to talk".
            // Use IsConnectedOrHandshaking, not IsConnected: a client reconnecting faster than
            // its previous handshake completes would otherwise stack new connections against a
            // peer stuck mid-handshake instead of replacing the stale one.
            if (peer.ConnectionState.Value.IsConnectingOrConnected()) {
                Log.LogWarning("'{PeerRef}': {Peer} is already connected, disconnecting the old connection first...",
                    rpcRef, peer);
                await peer.Disconnect(cancellationToken).ConfigureAwait(false);
            }

#if NET6_0_OR_GREATER
            var webSocketAcceptContext = Options.ConfigureWebSocket.Invoke(this, context, rpcRef);
            var acceptWebSocketTask = context.WebSockets.AcceptWebSocketAsync(webSocketAcceptContext);
#else
            var acceptWebSocketTask = context.WebSockets.AcceptWebSocketAsync();
#endif
            webSocket = await acceptWebSocketTask.ConfigureAwait(false);
            var properties = PropertyBag.Empty
                .KeylessSet((RpcPeer)peer)
                .KeylessSet(context)
                .KeylessSet(webSocket);
            var webSocketOwner = new WebSocketOwner(peer.Route.ToString(), webSocket, Services);
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
            if (e.IsCancellationOf(cancellationToken)) {
                Log.LogInformation(e, "'{PeerRef}': Normal RPC connection termination (via cancellation) for {Request}",
                    rpcRef, requestDescription);
                return; // Intended: this is typically a normal connection termination
            }

            if (connection is not null) {
                Log.LogInformation(e, "'{PeerRef}': Normal RPC connection termination for {Request}",
                    rpcRef, requestDescription);
                return; // Intended: this is typically a normal connection termination
            }

            Log.LogWarning(e, "'{PeerRef}': Failed to accept RPC connection for {Request}",
                rpcRef, requestDescription);
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
            webSocket?.Dispose();
        }
    }
}
