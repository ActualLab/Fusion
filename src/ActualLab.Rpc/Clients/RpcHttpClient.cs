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
    protected HttpClient HttpClient => field ??= Options.HttpClientFactory.Invoke(Services);

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
                "'{PeerRef}': No connection URL for ClientId='{ClientId}' - waiting for peer termination",
                clientPeer.Ref, clientPeer.ClientId);
            await TaskExt.NeverEnding(cancellationToken).ConfigureAwait(false);
        }

        Log.LogInformation(
            "'{PeerRef}': Connecting ClientId='{ClientId}' to {Url}",
            clientPeer.Ref, clientPeer.ClientId, uri);
        var hub = clientPeer.Hub;
        var connectTokenSource = new CancellationTokenSource();
        var connectToken = connectTokenSource.Token;
        _ = hub.SystemClock
            .Delay(hub.Limits.ConnectTimeout, cancellationToken)
            .ContinueWith(_ => connectTokenSource.CancelAndDisposeSilently(), TaskScheduler.Default);

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
            response = await HttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, connectToken)
                .ConfigureAwait(false);

            // If we're here, the response headers were received successfully.
            connectTokenSource.DisposeSilently();
        }
        catch (Exception e) {
            content.Complete(); // Unblocks DuplexHttpContent.SerializeToStreamAsync if it has already started
            request?.Dispose();
            if (e.IsCancellationOf(connectToken) && !cancellationToken.IsCancellationRequested)
                throw Errors.ConnectTimeout();

            Log.LogWarning(e, "'{PeerRef}': Failed to connect to {Url}", clientPeer.Ref, uri);
            throw;
        }
        finally {
            connectTokenSource.CancelAndDisposeSilently();
        }

        try {
            if (response.Version.Major < 2)
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
            var owner = new RpcHttpConnectionOwner(content, request, response);
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
            Log.LogWarning(e, "'{PeerRef}': Failed to connect to {Url}", clientPeer.Ref, uri);
            throw;
        }
    }
}
