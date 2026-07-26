using System.IO.Pipelines;
using System.Net;
using ActualLab.Rpc.Clients.Internal;
using ActualLab.Rpc.Infrastructure;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Clients;

/// <summary>
/// An <see cref="RpcClient"/> implementation that establishes connections via full-duplex HTTP/2 requests.
/// </summary>
public class RpcHttpClient(IServiceProvider services) : RpcClient(services)
{
    public RpcHttpClientOptions Options { get; } = services.GetRequiredService<RpcHttpClientOptions>();

    // Every RPC connection holds its request open for as long as the connection lives, and
    // SocketsHttpHandler can't carry two such requests over one HTTP/2 connection: the second one
    // never receives its response headers. Sharing a single HttpClient would multiplex them onto
    // one connection, so each connection gets its own - disposed together with it.
    protected virtual HttpClient CreateHttpClient()
        => Options.HttpClientFactory.Invoke(Services);

    public override Task<RpcConnection> ConnectRemote(
        RpcClientPeer clientPeer,
        RpcPeerConnectionState connectionState,
        CancellationToken cancellationToken)
    {
        var uri = Options.ConnectionUriResolver.Invoke(clientPeer);
        return ConnectRemote(clientPeer, uri, cancellationToken);
    }

    public virtual async Task<RpcConnection> ConnectRemote(
        RpcClientPeer clientPeer, Uri? uri, CancellationToken cancellationToken)
    {
        if (uri is null) {
            // The expected behavior for null URI is to wait indefinitely
            Log.LogWarning(
                "'{Route}': No connection URL for ClientId='{ClientId}' - waiting for peer termination",
                clientPeer.Route, clientPeer.ClientId);
            await TaskExt.NeverEnding(cancellationToken).ConfigureAwait(false);
        }

        Log.LogInformation(
            "'{Route}': Connecting ClientId='{ClientId}' to {Url}",
            clientPeer.Route, clientPeer.ClientId, uri);
        var hub = clientPeer.Hub;
        var connectTokenSource = new CancellationTokenSource();
        var connectToken = connectTokenSource.Token;
        _ = hub.SystemClock
            .Delay(hub.Limits.ConnectTimeout, cancellationToken)
            .ContinueWith(_ => connectTokenSource.CancelAndDisposeSilently(), TaskScheduler.Default);
        // The request is sent with this token rather than connectToken, because it has to outlive
        // the connect: cancelling it is the only way to reset the request's HTTP/2 stream once the
        // connection is closed. Gracefully ending the request body instead leaves the stream
        // half-open, and the server's undrained response then blocks every other stream sharing
        // that connection - see RpcHttpConnectionOwner.
        var requestTokenSource = connectToken.CreateLinkedTokenSource();
        var requestToken = requestTokenSource.Token;

        var httpClient = CreateHttpClient();
        var content = new DuplexHttpContent();
        HttpRequestMessage? request = null;
        HttpResponseMessage response;
        try {
            // HTTP/2 over cleartext requires "prior knowledge" (RequestVersionExact);
            // over TLS we allow HTTP/2 or higher, since ALPN negotiates the version.
            var isHttps = string.Equals(uri!.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            request = new HttpRequestMessage(HttpMethod.Post, uri) {
                Version = HttpVersion.Version20,
                VersionPolicy = isHttps
                    ? HttpVersionPolicy.RequestVersionOrHigher
                    : HttpVersionPolicy.RequestVersionExact,
                Content = content,
            };
            response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken)
                .ConfigureAwait(false);

            // If we're here, the response headers were received successfully.
            connectTokenSource.DisposeSilently();
        }
        catch (Exception e) {
            content.Complete(); // Unblocks DuplexHttpContent.SerializeToStreamAsync if it has already started
            request?.Dispose();
            requestTokenSource.CancelAndDisposeSilently();
            httpClient.Dispose();
            if (e.IsCancellationOf(requestToken) && !cancellationToken.IsCancellationRequested)
                throw Errors.ConnectTimeout();

            Log.LogWarning(e, "'{Route}': Failed to connect to {Url}", clientPeer.Route, uri);
            throw;
        }
        finally {
            connectTokenSource.CancelAndDisposeSilently();
        }

        try {
            if (Options.MustRequireHttp2 && response.Version.Major < 2)
                throw Errors.Http2ConnectionRequired(response.Version);
            if (response.StatusCode == HttpStatusCode.UnsupportedMediaType)
                throw Errors.UnsupportedSerializationFormat(clientPeer.SerializationFormat.Key);

            response.EnsureSuccessStatusCode();

            var requestStream = await content.WhenStreamReady.ConfigureAwait(false);
            var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var properties = PropertyBag.Empty
                .KeylessSet((RpcPeer)clientPeer)
                .KeylessSet(uri)
                .KeylessSet(response);
            var owner = new RpcHttpConnectionOwner(content, request, response, requestTokenSource, httpClient);
            RpcTransport transport;
            if (Options.UsePipes) {
                var pipeOptions = Options.PipeTransportOptionsFactory.Invoke(clientPeer, properties);
                var pipeReader = PipeReader.Create(responseStream);
                var pipeWriter = PipeWriter.Create(requestStream);
                transport = new RpcPipeTransport(pipeOptions, clientPeer, pipeReader, pipeWriter) { Owner = owner };
            }
            else {
                var streamOptions = Options.StreamTransportOptionsFactory.Invoke(clientPeer, properties);
                transport = new RpcStreamTransport(streamOptions, clientPeer, responseStream, requestStream) { Owner = owner };
            }
            return new RpcConnection(transport, properties);
        }
        catch (Exception e) {
            content.Complete();
            request.Dispose();
            response.Dispose();
            requestTokenSource.CancelAndDisposeSilently();
            httpClient.Dispose();
            Log.LogWarning(e, "'{Route}': Failed to connect to {Url}", clientPeer.Route, uri);
            throw;
        }
    }
}
