using System.Globalization;
using System.Net;
using ActualLab.Compliance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

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
    public RpcHttpServerRefFactory RefFactory { get; } = services.GetRequiredService<RpcHttpServerRefFactory>();

    public virtual async Task Invoke(HttpContext context, bool isBackend)
    {
        var request = context.Request;
        var uri = new UriBuilder(
            request.Scheme,
            request.Host.Host,
            request.Host.Port ?? -1,
            request.Path,
            Sanitizer.Sanitize<Sanitizers.RpcRequestQuery>(request.QueryString.Value ?? ""));
        var requestDescription = $"{request.Method} {uri}";
        var cancellationToken = context.RequestAborted;
        var maxRequestBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxRequestBodySizeFeature is { IsReadOnly: false })
            maxRequestBodySizeFeature.MaxRequestBodySize = null;
        // The request body stays open for the connection's lifetime and carries nothing at all while
        // the peer is idle, so Kestrel's slowloris guard (MinRequestBodyDataRate: 240 B/s past a 5s
        // grace period) aborts it a few seconds into every quiet period. On HTTP/2 that abort is
        // connection-level, so it also kills every other RPC stream a proxy multiplexed onto the
        // same backend connection.
        if (context.Features.Get<IHttpMinRequestBodyDataRateFeature>() is { } minRequestBodyDataRateFeature)
            minRequestBodyDataRateFeature.MinDataRate = null;

        // Full-duplex RPC requires HTTP/2 (or higher) - HTTP/1.x can't read the request while writing the response
        if (Options.MustRequireHttp2 && !IsHttp2OrHigher(request.Protocol)) {
            Log.LogWarning("HTTP/2 request expected, but got {Request} ({Protocol})",
                requestDescription, request.Protocol);
            context.Response.StatusCode = (int)HttpStatusCode.UpgradeRequired;
            return;
        }

        RpcConnection? connection = null;
        RpcRef? rpcRef = null;
        try {
            rpcRef = RefFactory.Invoke(this, context, isBackend).RequireServer();

            // Runs before GetServerPeer, before the response is started and before any Disconnect,
            // so a request that fails the proof can't evict the incumbent connection
            // and can't create a peer. See RpcWebSocketServer.Invoke for the full rationale.
            if (!TryVerifyReconnectProof(context, rpcRef)) {
                Log.LogWarning("'{PeerRef}': Rejected RPC connection: invalid reconnect proof for {Request}",
                    rpcRef, requestDescription);
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            if (Hub.SerializationFormats.GetClientRejectionReason(rpcRef.SerializationFormat) is { } rejectionReason) {
                Log.LogWarning("'{PeerRef}': Rejected {Request}: {Reason}",
                    rpcRef, requestDescription, rejectionReason);
                context.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
                return;
            }

            Log.LogInformation("'{PeerRef}': Accepting RPC connection for {Request}", rpcRef, requestDescription);
            var peer = Hub.GetServerPeer(rpcRef);

            // Disconnect any stale connection BEFORE starting the response.
            // See RpcWebSocketServer.Invoke for the detailed rationale.
            if (peer.ConnectionState.Value.IsConnectingOrConnected()) {
                Log.LogWarning("'{PeerRef}': {Peer} is already connected, disconnecting the old connection first...",
                    rpcRef, peer);
                await peer.Disconnect(cancellationToken).ConfigureAwait(false);
            }

            // Start the response right away, so the client's SendAsync (ResponseHeadersRead) completes fast
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/octet-stream";
            await context.Response.StartAsync(cancellationToken).ConfigureAwait(false);

            var properties = PropertyBag.Empty
                .KeylessSet((RpcPeer)peer)
                .KeylessSet(context);
            var stopTokenSource = cancellationToken.CreateLinkedTokenSource();
            RpcTransport transport;
            Task whenClosed;
            if (Options.UsePipes) {
                var pipeOptions = HttpClientOptions.PipeTransportOptionsFactory.Invoke(peer, properties);
                var pipeTransport = new RpcPipeTransport(
                    pipeOptions, peer, request.BodyReader, context.Response.BodyWriter, stopTokenSource);
                transport = pipeTransport;
                whenClosed = pipeTransport.WhenClosed;
            }
            else {
                var streamOptions = HttpClientOptions.StreamTransportOptionsFactory.Invoke(peer, properties);
                var streamTransport = new RpcStreamTransport(
                    streamOptions, peer, request.Body, context.Response.Body, stopTokenSource);
                transport = streamTransport;
                whenClosed = streamTransport.WhenClosed;
            }
            connection = await PeerOptions.ServerConnectionFactory
                .Invoke(peer, transport, properties, cancellationToken)
                .ConfigureAwait(false);

            await peer.SetNextConnection(connection, cancellationToken).ConfigureAwait(false);
            await whenClosed.ConfigureAwait(false);
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
            try {
                if (!context.Response.HasStarted)
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            catch {
                // Intended
            }
        }
    }

    // Protected methods

    protected virtual bool TryVerifyReconnectProof(HttpContext context, RpcRef rpcRef)
    {
        var query = context.Request.Query;
        var counterText = query[Options.ReconnectProofCounterParameterName].SingleOrDefault() ?? "";
        var proof = query[Options.ReconnectProofParameterName].SingleOrDefault() ?? "";
        // rpcRef.HostInfo is the raw clientId, so the value in the HMAC is the one that selected the peer
        Hub.TryGetServerPeer(rpcRef, out var peer);
        return RpcReconnectProof.TryVerify(
            peer, rpcRef.HostInfo, counterText, proof, Options.RequireReconnectProof);
    }

    // Private methods

    private static bool IsHttp2OrHigher(string protocol)
    {
        const string prefix = "HTTP/";
        if (!protocol.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var versionText = protocol.AsSpan(prefix.Length);
        var dotIndex = versionText.IndexOf('.');
        if (dotIndex >= 0)
            versionText = versionText[..dotIndex];
        return int.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            && major >= 2;
    }
}
