using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Server-side handler that accepts incoming full-duplex HTTP/2 connections and establishes
/// RPC peer connections for ASP.NET Core hosts.
/// </summary>
public class RpcHttpServer(RpcHttpServerOptions options, IServiceProvider services)
    : RpcServiceBase(services)
{
    public RpcHttpServerOptions Options { get; } = options;
    public RpcPeerOptions PeerOptions { get; } = services.GetRequiredService<RpcPeerOptions>();
    public RpcHttpClientOptions HttpClientOptions { get; } = services.GetRequiredService<RpcHttpClientOptions>();
    public RpcHttpServerPeerRefFactory PeerRefFactory { get; } = services.GetRequiredService<RpcHttpServerPeerRefFactory>();

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
        var maxRequestBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxRequestBodySizeFeature is { IsReadOnly: false })
            maxRequestBodySizeFeature.MaxRequestBodySize = null;

        // Full-duplex RPC requires HTTP/2 (or higher) - HTTP/1.x can't read the request while writing the response
        if (HttpProtocol.IsHttp10(request.Protocol) || HttpProtocol.IsHttp11(request.Protocol)) {
            Log.LogWarning("HTTP/2 request expected, but got {Request} ({Protocol})",
                requestDescription, request.Protocol);
            context.Response.StatusCode = (int)HttpStatusCode.UpgradeRequired;
            return;
        }

        RpcConnection? connection = null;
        RpcPeerRef? peerRef = null;
        try {
            peerRef = PeerRefFactory.Invoke(this, context, isBackend).RequireServer();
            if (!Hub.SerializationFormats.TryGet(peerRef.SerializationFormat, out _)) {
                Log.LogWarning("'{PeerRef}': Unsupported RPC serialization format '{Format}' for {Request}",
                    peerRef, peerRef.SerializationFormat, requestDescription);
                context.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
                return;
            }

            Log.LogInformation("'{PeerRef}': Accepting RPC connection for {Request}", peerRef, requestDescription);
            var peer = Hub.GetServerPeer(peerRef);

            // Disconnect any stale connection BEFORE starting the response.
            // See RpcWebSocketServer.Invoke for the detailed rationale.
            if (peer.IsConnectedOrHandshaking()) {
                Log.LogWarning("'{PeerRef}': {Peer} is already connected, disconnecting the old connection first...",
                    peerRef, peer);
                await peer.Disconnect(cancellationToken).ConfigureAwait(false);
            }

            // Start the response right away, so the client's SendAsync (ResponseHeadersRead) completes fast
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/octet-stream";
            await context.Response.StartAsync(cancellationToken).ConfigureAwait(false);

            var properties = PropertyBag.Empty
                .KeylessSet((RpcPeer)peer)
                .KeylessSet(context);
            var transportOptions = HttpClientOptions.PipeTransportOptionsFactory.Invoke(peer, properties);
            var stopTokenSource = cancellationToken.CreateLinkedTokenSource();
            var pipeReader = request.BodyReader;
            var pipeWriter = context.Response.BodyWriter;
            var transport = new RpcPipeTransport(transportOptions, peer, pipeReader, pipeWriter, stopTokenSource);
            connection = await PeerOptions.ServerConnectionFactory
                .Invoke(peer, transport, properties, cancellationToken)
                .ConfigureAwait(false);

            await peer.SetNextConnection(connection, cancellationToken).ConfigureAwait(false);
            await transport.WhenClosed.ConfigureAwait(false);
        }
        catch (Exception e) {
            if (e.IsCancellationOf(cancellationToken)) {
                Log.LogInformation(e, "'{PeerRef}': Normal RPC connection termination (via cancellation) for {Request}",
                    peerRef, requestDescription);
                return; // Intended: this is typically a normal connection termination
            }

            if (connection is not null) {
                Log.LogInformation(e, "'{PeerRef}': Normal RPC connection termination for {Request}",
                    peerRef, requestDescription);
                return; // Intended: this is typically a normal connection termination
            }

            Log.LogWarning(e, "'{PeerRef}': Failed to accept RPC connection for {Request}",
                peerRef, requestDescription);
            try {
                if (!context.Response.HasStarted)
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            catch {
                // Intended
            }
        }
    }
}
