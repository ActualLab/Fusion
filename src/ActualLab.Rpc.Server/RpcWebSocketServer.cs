using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc.Server;

public class RpcWebSocketServer(RpcWebSocketServerOptions options, IServiceProvider services)
    : RpcServiceBase(services)
{
    public RpcWebSocketServerOptions Options { get; } = options;
    public RpcPeerOptions PeerOptions { get; } = services.GetRequiredService<RpcPeerOptions>();
    public RpcWebSocketClientOptions WebSocketClientOptions { get; } = services.GetRequiredService<RpcWebSocketClientOptions>();
    public RpcWebSocketServerPeerRefFactory PeerRefFactory { get; } = services.GetRequiredService<RpcWebSocketServerPeerRefFactory>();

    public virtual async Task Invoke(HttpContext context, bool isBackend)
    {
        var cancellationToken = context.RequestAborted;
        if (!context.WebSockets.IsWebSocketRequest) {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        WebSocket? webSocket = null;
        RpcConnection? connection = null;
        try {
            var peerRef = PeerRefFactory.Invoke(this, context, isBackend).RequireServer();
            var peer = Hub.GetServerPeer(peerRef);

#if NET6_0_OR_GREATER
            var webSocketAcceptContext = Options.ConfigureWebSocket.Invoke();
            var acceptWebSocketTask = context.WebSockets.AcceptWebSocketAsync(webSocketAcceptContext);
#else
            var acceptWebSocketTask = context.WebSockets.AcceptWebSocketAsync();
#endif
            webSocket = await acceptWebSocketTask.ConfigureAwait(false);
            var properties = PropertyBag.Empty
                .KeylessSet((RpcPeer)peer)
                .KeylessSet(context)
                .KeylessSet(webSocket);
            var webSocketOwner = new WebSocketOwner(peer.Ref.ToString(), webSocket, Services);
            var webSocketChannelOptions = WebSocketClientOptions.WebSocketChannelOptionsFactory.Invoke(peer, properties);
            var channel = new WebSocketChannel<RpcMessage>(
                webSocketChannelOptions, webSocketOwner, cancellationToken) {
                OwnsWebSocketOwner = false,
            };
            connection = await PeerOptions.ServerConnectionFactory
                .Invoke(peer, channel, properties, cancellationToken)
                .ConfigureAwait(false);

            if (peer.IsConnected()) {
                var delay = Options.ChangeConnectionDelay;
                Log.LogWarning("{Peer} is already connected, will change its connection in {Delay}...",
                    peer, delay.ToShortString());
                await peer.Hub.Clock.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            await peer.SetConnection(connection, cancellationToken).ConfigureAwait(false);
            await channel.WhenClosed.ConfigureAwait(false);
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
            webSocket?.Dispose();
        }
    }
}
