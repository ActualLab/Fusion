using System.Net.WebSockets;
using System.Text.Encodings.Web;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc.Clients;

public record RpcWebSocketClientOptions
{
    public static RpcWebSocketClientOptions Default { get; set; } = new();

    public string RequestPath { get; init; } = "/rpc/ws";
    public string BackendRequestPath { get; init; } = "/backend/rpc/ws";
    public string SerializationFormatParameterName { get; init; } = "f";
    public string ClientIdParameterName { get; init; } = "clientId";
    public bool UseAutoFrameDelayerFactory { get; init; } = false;

    // Delegate options
    public Func<RpcClientPeer, string> HostUrlResolver { get; init; }
    public Func<RpcClientPeer, Uri?> ConnectionUriResolver { get; init; }
    public Func<RpcPeer, PropertyBag, WebSocketRpcTransport.Options> WebSocketTransportOptionsFactory { get; init; }
    public Func<RpcClientPeer, WebSocketOwner> WebSocketOwnerFactory { get; init; }
    public Func<FrameDelayer?>? FrameDelayerFactory { get; init; }

    // ReSharper disable once ConvertConstructorToMemberInitializers
    public RpcWebSocketClientOptions()
    {
        HostUrlResolver = DefaultHostUrlResolver;
        ConnectionUriResolver = DefaultConnectionUriResolver;
        WebSocketTransportOptionsFactory = DefaultWebSocketTransportOptionsFactory;
        WebSocketOwnerFactory = DefaultWebSocketOwnerFactory;
        FrameDelayerFactory = FrameDelayerFactories.None;
    }

    // Protected methods

    protected static string DefaultHostUrlResolver(RpcClientPeer peer)
        => peer.Ref.HostInfo;

    protected static Uri? DefaultConnectionUriResolver(RpcClientPeer peer)
    {
        var options = peer.Hub.Services.GetRequiredService<RpcWebSocketClientOptions>();
        var url = options.HostUrlResolver.Invoke(peer).TrimSuffix("/");
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
                ? options.BackendRequestPath
                : options.RequestPath;
            url += requestPath;
        }

#pragma warning disable CA1307
        var queryStart = url.IndexOf('?') < 0 ? '?' : '&';
#pragma warning restore CA1307
        url = $"{url}{queryStart}{options.ClientIdParameterName}={UrlEncoder.Default.Encode(peer.ClientId)}"
            + $"&{options.SerializationFormatParameterName}={peer.SerializationFormat.Key}";
        return new Uri(url, UriKind.Absolute);
    }

    protected static WebSocketRpcTransport.Options DefaultWebSocketTransportOptionsFactory(
        RpcPeer peer, PropertyBag properties)
        => WebSocketRpcTransport.Options.Default;

    private WebSocketOwner DefaultWebSocketOwnerFactory(RpcClientPeer peer)
    {
        var clientWebSocket = new ClientWebSocket();
        return new WebSocketOwner(peer.Ref.ToString(), clientWebSocket, peer.Hub.Services);
    }
}
