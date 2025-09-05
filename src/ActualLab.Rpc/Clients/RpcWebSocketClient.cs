using System.Net.WebSockets;
using System.Text.Encodings.Web;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.WebSockets;

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
        public Func<RpcWebSocketClient, RpcClientPeer, Uri?> ConnectionUriResolver { get; init; }
            = DefaultConnectionUriResolver;
        public Func<RpcWebSocketClient, RpcClientPeer, WebSocketOwner> WebSocketOwnerFactory { get; init; }
            = DefaultWebSocketOwnerFactory;

        public string RequestPath { get; init; } = "/rpc/ws";
        public string BackendRequestPath { get; init; } = "/backend/rpc/ws";
        public string SerializationFormatParameterName { get; init; } = "f";
        public string ClientIdParameterName { get; init; } = "clientId";

        public static string DefaultHostUrlResolver(RpcWebSocketClient client, RpcClientPeer peer)
            => peer.Ref.HostInfo;

        public static Uri? DefaultConnectionUriResolver(RpcWebSocketClient client, RpcClientPeer peer)
        {
            var settings = client.Settings;
            var url = settings.HostUrlResolver.Invoke(client, peer).TrimSuffix("/");
            if (url.IsNullOrEmpty())
                return null;

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

            var queryStart = url.IndexOf('?') < 0 ? '?' : '&';
            url = $"{url}{queryStart}{settings.ClientIdParameterName}={UrlEncoder.Default.Encode(peer.ClientId)}"
                + $"&{settings.SerializationFormatParameterName}={peer.SerializationFormat.Key}";
            return new Uri(url, UriKind.Absolute);
        }

        public static WebSocketOwner DefaultWebSocketOwnerFactory(RpcWebSocketClient client, RpcClientPeer peer)
        {
            var ws = new ClientWebSocket();
            return new WebSocketOwner(peer.Ref.ToString(), ws, client.Services);
        }
    }

    public Options Settings { get; } = settings;
    public RpcWebSocketChannelOptionsProvider WebSocketChannelOptionsProvider { get; }
        = services.GetRequiredService<RpcWebSocketChannelOptionsProvider>();

    public override Task<RpcConnection> ConnectRemote(RpcClientPeer clientPeer, CancellationToken cancellationToken)
    {
        var uri = Settings.ConnectionUriResolver(this, clientPeer);
        return ConnectRemote(clientPeer, uri, cancellationToken);
    }

    public virtual async Task<RpcConnection> ConnectRemote(
        RpcClientPeer clientPeer, Uri? uri, CancellationToken cancellationToken)
    {
        if (uri is null) {
            // The expected behavior for null URI is to wait indefinitely
            Log.LogWarning("'{PeerRef}': No connection URL - waiting for peer termination", clientPeer.Ref);
            await TaskExt.NeverEnding(cancellationToken).ConfigureAwait(false);
        }

        // Log.LogInformation("'{PeerRef}': Connection URL: {Url}", clientPeer.Ref, uri);
        var hub = clientPeer.Hub;
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
                        o = Settings.WebSocketOwnerFactory.Invoke(this, clientPeer);
                        await o.ConnectAsync(uri!, connectToken).ConfigureAwait(false);
                        return o;
                    }
                    catch when (o is not null) {
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

        var properties = PropertyBag.Empty
            .KeylessSet((RpcPeer)clientPeer)
            .KeylessSet(uri)
            .KeylessSet(webSocketOwner)
            .KeylessSet(webSocketOwner.WebSocket);
        var webSocketChannelOptions = WebSocketChannelOptionsProvider.Invoke(clientPeer, properties);
        var channel = new WebSocketChannel<RpcMessage>(webSocketChannelOptions, webSocketOwner);
        return new RpcConnection(channel, properties);
    }
}
