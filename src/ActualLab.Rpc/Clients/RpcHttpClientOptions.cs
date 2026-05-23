using System.Text.Encodings.Web;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Clients;

/// <summary>
/// Configuration options for <see cref="RpcHttpClient"/>, including URL resolution and transport setup.
/// </summary>
public record RpcHttpClientOptions
{
    public static RpcHttpClientOptions Default { get; set; } = new();

    public string RequestPath { get; init; } = "/rpc/http";
    public string BackendRequestPath { get; init; } = "/backend/rpc/http";
    public string SerializationFormatParameterName { get; init; } = "f";
    public string ClientIdParameterName { get; init; } = "clientId";
    public bool UseAutoFrameDelayerFactory { get; init; } = false;
    public bool UsePipes { get; init; } = true;

    // Delegate options
    public Func<RpcClientPeer, string> HostUrlResolver { get; init; }
    public Func<RpcClientPeer, Uri?> ConnectionUriResolver { get; init; }
    public Func<RpcPeer, PropertyBag, RpcPipeTransport.Options> PipeTransportOptionsFactory { get; init; }
    public Func<RpcPeer, PropertyBag, RpcStreamTransport.Options> StreamTransportOptionsFactory { get; init; }
    public Func<IServiceProvider, HttpClient> HttpClientFactory { get; init; }
    public Func<RpcFrameDelayer?>? FrameDelayerFactory { get; init; }

    // ReSharper disable once ConvertConstructorToMemberInitializers
    public RpcHttpClientOptions()
    {
        HostUrlResolver = DefaultHostUrlResolver;
        ConnectionUriResolver = DefaultConnectionUriResolver;
        PipeTransportOptionsFactory = DefaultPipeTransportOptionsFactory;
        StreamTransportOptionsFactory = DefaultStreamTransportOptionsFactory;
        HttpClientFactory = DefaultHttpClientFactory;
        FrameDelayerFactory = RpcFrameDelayerFactories.None;
    }

    // Protected methods

    protected static string DefaultHostUrlResolver(RpcClientPeer peer)
        => peer.Ref.HostInfo;

    protected static Uri? DefaultConnectionUriResolver(RpcClientPeer peer)
    {
        var options = peer.Hub.Services.GetRequiredService<RpcHttpClientOptions>();
        var url = options.HostUrlResolver.Invoke(peer).TrimSuffix("/");
        if (url.IsNullOrEmpty())
            return null;

        // Prepend https:// if the host URL has no scheme
        if (!url.StartsWith("http://", StringComparison.Ordinal)
            && !url.StartsWith("https://", StringComparison.Ordinal))
            url = "https://" + url;

        // Append the RPC request path if the host URL doesn't already carry one
        if (new Uri(url, UriKind.Absolute).AbsolutePath.Length <= 1) {
            var requestPath = peer.Ref.IsBackend
                ? options.BackendRequestPath
                : options.RequestPath;
            url += requestPath;
        }

        var queryStart = url.IndexOf('?', StringComparison.Ordinal) < 0 ? '?' : '&';
        url = $"{url}{queryStart}{options.ClientIdParameterName}={UrlEncoder.Default.Encode(peer.ClientId)}"
            + $"&{options.SerializationFormatParameterName}={peer.SerializationFormat.Key}";
        return new Uri(url, UriKind.Absolute);
    }

    protected static RpcPipeTransport.Options DefaultPipeTransportOptionsFactory(
        RpcPeer peer, PropertyBag properties)
    {
        var options = peer.Hub.Services.GetRequiredService<RpcHttpClientOptions>();
        var frameDelayerFactory = options.UseAutoFrameDelayerFactory
            ? RpcFrameDelayerFactories.Auto(peer, properties)
            : options.FrameDelayerFactory;
        return RpcPipeTransport.Options.Default with {
            FrameDelayerFactory = frameDelayerFactory,
        };
    }

    protected static RpcStreamTransport.Options DefaultStreamTransportOptionsFactory(
        RpcPeer peer, PropertyBag properties)
    {
        var options = peer.Hub.Services.GetRequiredService<RpcHttpClientOptions>();
        var frameDelayerFactory = options.UseAutoFrameDelayerFactory
            ? RpcFrameDelayerFactories.Auto(peer, properties)
            : options.FrameDelayerFactory;
        return RpcStreamTransport.Options.Default with {
            FrameDelayerFactory = frameDelayerFactory,
        };
    }

    protected static HttpClient DefaultHttpClientFactory(IServiceProvider services)
        => new(new SocketsHttpHandler {
            EnableMultipleHttp2Connections = true,
        });
}
