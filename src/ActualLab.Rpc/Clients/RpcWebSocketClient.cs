using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text.Encodings.Web;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.WebSockets;
using UnreferencedCode = ActualLab.Internal.UnreferencedCode;

namespace ActualLab.Rpc.Clients;

public class RpcWebSocketClient(
    RpcWebSocketClient.Options settings,
    IServiceProvider services
    ) : RpcClient(services)
{
    public record Options
    {
        public static Options Default { get; set; } = new();

        public Func<RpcWebSocketClient, RpcClientPeer, string> HostUrlResolver { get; init; }
            = DefaultHostUrlResolver;
        public Func<RpcWebSocketClient, RpcClientPeer, Uri> ConnectionUriResolver { get; init; }
            = DefaultConnectionUriResolver;
        public Func<RpcWebSocketClient, RpcClientPeer, WebSocketOwner> WebSocketOwnerFactory { get; init; }
            = DefaultWebSocketOwnerFactory;

        public string RequestPath { get; init; } = "/rpc/ws";
        public string BackendRequestPath { get; init; } = "/backend/rpc/ws";
        public string ClientIdParameterName { get; init; } = "clientId";
#pragma warning disable IL2026
        public Func<RpcWebSocketClient, WebSocketOwner, ImmutableOptionSet, WebSocketChannel<RpcMessage>>
            WebSocketChannelFactory { get; init; } = DefaultWebSocketChannelFactory;
#pragma warning restore IL2026

        public static string DefaultHostUrlResolver(RpcWebSocketClient client, RpcClientPeer peer)
            => peer.Ref.Key.Value;

        public static Uri DefaultConnectionUriResolver(RpcWebSocketClient client, RpcClientPeer peer)
        {
            var settings = client.Settings;
            var url = settings.HostUrlResolver.Invoke(client, peer).TrimSuffix("/");
            var isWebSocketUrl = url.StartsWith("ws://", StringComparison.Ordinal)
                || url.StartsWith("wss://", StringComparison.Ordinal);
            if (!isWebSocketUrl) {
                if (url.StartsWith("http://", StringComparison.Ordinal))
                    url = "ws://" + url[7..];
                else if (url.StartsWith("https://", StringComparison.Ordinal))
                    url = "wss://" + url[8..];
                else
                    url = "wss://" + url;
                var requestPath = peer.Ref.IsBackend
                    ? settings.BackendRequestPath
                    : settings.RequestPath;
                url += requestPath;
            }

            var uriBuilder = new UriBuilder(url);
            var queryTail = $"{settings.ClientIdParameterName}={UrlEncoder.Default.Encode(peer.ClientId)}";
            if (!uriBuilder.Query.IsNullOrEmpty())
                uriBuilder.Query += "&" + queryTail;
            else
                uriBuilder.Query = queryTail;
            return uriBuilder.Uri;
        }

        public static WebSocketOwner DefaultWebSocketOwnerFactory(RpcWebSocketClient client, RpcClientPeer peer)
            => new(peer.Ref.Key, new ClientWebSocket(), client.Services);

        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        public static WebSocketChannel<RpcMessage> DefaultWebSocketChannelFactory(
            RpcWebSocketClient client, WebSocketOwner webSocketOwner, ImmutableOptionSet options)
            => new(WebSocketChannel<RpcMessage>.Options.Default, webSocketOwner);
    }

    public Options Settings { get; } = settings;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override Task<RpcConnection> Connect(RpcClientPeer peer, CancellationToken cancellationToken)
    {
        var uri = Settings.ConnectionUriResolver(this, peer);
        return Connect(peer, uri, cancellationToken);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public virtual async Task<RpcConnection> Connect(
        RpcClientPeer peer, Uri uri, CancellationToken cancellationToken)
    {
        var hub = peer.Hub;
        var connectCts = new CancellationTokenSource();
        var connectToken = connectCts.Token;
        _ = hub.Clock
            .Delay(hub.Limits.ConnectTimeout, cancellationToken)
            .ContinueWith(_ => connectCts.CancelAndDisposeSilently(), TaskScheduler.Default);
        WebSocketOwner webSocketOwner;
        try {
            webSocketOwner = await Task
                .Run(async () => {
                    WebSocketOwner? o = null;
                    try {
                        o = Settings.WebSocketOwnerFactory.Invoke(this, peer);
                        await o.ConnectAsync(uri, connectToken).ConfigureAwait(false);
                        return o;
                    }
                    catch when (o != null) {
                        try {
                            await o.DisposeAsync().ConfigureAwait(false);
                        }
                        catch {
                            // Intended
                        }
                        throw;
                    }
                }, connectToken)
                .WaitAsync(connectToken) // MAUI sometimes stucks in the sync part of ConnectAsync
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            if (!cancellationToken.IsCancellationRequested && connectToken.IsCancellationRequested)
                throw Errors.ConnectTimeout();
            throw;
        }

        var options = ImmutableOptionSet.Empty
            .Set((RpcPeer)peer)
            .Set(uri)
            .Set(webSocketOwner)
            .Set(webSocketOwner.WebSocket);
        var channel = Settings.WebSocketChannelFactory.Invoke(this, webSocketOwner, options);
        return new RpcConnection(channel, options);
    }
}
