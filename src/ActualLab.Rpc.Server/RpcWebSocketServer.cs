using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.WebSockets;

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
        public TimeSpan ChangeConnectionDelay { get; init; } = TimeSpan.FromSeconds(1);
        public WebSocketChannel<RpcMessage>.Options WebSocketChannelOptions { get; init; } = WebSocketChannel<RpcMessage>.Options.Default;
#if NET6_0_OR_GREATER
        public Func<WebSocketAcceptContext> ConfigureWebSocket { get; init; } = () => new();
#endif
    }

    public Options Settings { get; } = settings;
    public RpcWebSocketServerPeerRefFactory PeerRefFactory { get; }
        = services.GetRequiredService<RpcWebSocketServerPeerRefFactory>();
    public RpcServerConnectionFactory ServerConnectionFactory { get; }
        = services.GetRequiredService<RpcServerConnectionFactory>();

    public async Task Invoke(HttpContext context, bool isBackend)
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
            if (peer.ConnectionState.Value.IsConnected()) {
                var delay = Settings.ChangeConnectionDelay;
                Log.LogWarning("{Peer} is already connected, will change its connection in {Delay}...",
                    peer, delay.ToShortString());
                await peer.Hub.Clock.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

#if NET6_0_OR_GREATER
            var webSocketAcceptContext = Settings.ConfigureWebSocket.Invoke();
            var acceptWebSocketTask = context.WebSockets.AcceptWebSocketAsync(webSocketAcceptContext);
#else
            var acceptWebSocketTask = context.WebSockets.AcceptWebSocketAsync();
#endif
            webSocket = await acceptWebSocketTask.ConfigureAwait(false);
            var webSocketOwner = new WebSocketOwner(peer.Ref.Key, webSocket, Services);
            var channel = new WebSocketChannel<RpcMessage>(
                Settings.WebSocketChannelOptions, webSocketOwner, cancellationToken) {
                OwnsWebSocketOwner = false,
            };
            var options = ImmutableOptionSet.Empty
                .Set((RpcPeer)peer)
                .Set(context)
                .Set(webSocket);
            connection = await ServerConnectionFactory
                .Invoke(peer, channel, options, cancellationToken)
                .ConfigureAwait(false);
            peer.SetConnection(connection);
            await channel.WhenClosed.ConfigureAwait(false);
        }
        catch (Exception e) {
            if (connection != null || e.IsCancellationOf(cancellationToken))
                return; // Intended: this is typically a normal connection termination

            var request = context.Request;
            Log.LogWarning(e, "Failed to accept RPC connection: {Path}{Query}", request.Path, request.QueryString);
            if (webSocket != null)
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
