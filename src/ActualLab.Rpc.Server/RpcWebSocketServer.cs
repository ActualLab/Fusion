using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Server-side handler that accepts incoming WebSocket connections and establishes
/// RPC peer connections for ASP.NET Core hosts.
/// </summary>
public class RpcWebSocketServer(RpcWebSocketServerOptions options, IServiceProvider services)
    : RpcServiceBase(services)
{
    private const string OriginHeaderName = "Origin";

    public RpcWebSocketServerOptions Options { get; } = options;
    public RpcPeerOptions PeerOptions { get; } = services.GetRequiredService<RpcPeerOptions>();
    public RpcWebSocketClientOptions WebSocketClientOptions { get; } = services.GetRequiredService<RpcWebSocketClientOptions>();
    public RpcWebSocketServerRefFactory RefFactory { get; } = services.GetRequiredService<RpcWebSocketServerRefFactory>();

    public virtual async Task Invoke(HttpContext context, bool isBackend)
    {
        var request = context.Request;
        var uri = new UriBuilder(
            request.Scheme,
            request.Host.Host,
            request.Host.Port ?? -1,
            request.Path,
            RpcQuerySanitizer.Sanitize(request.QueryString.Value));
        var requestDescription = $"{request.Method} {uri}";
        var cancellationToken = context.RequestAborted;
        if (!context.WebSockets.IsWebSocketRequest) {
            Log.LogWarning("WebSocket request expected, but got {Request}", requestDescription);
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        // Runs before RefFactory, so a rejected request creates no server peer
        var origin = request.Headers[OriginHeaderName].ToString();
        if (!Options.OriginValidator.Invoke(this, context, origin)) {
            Log.LogWarning("Rejected RPC connection from origin '{Origin}' for {Request}",
                origin, requestDescription);
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return;
        }

        WebSocket? webSocket = null;
        RpcConnection? connection = null;
        RpcRef? rpcRef = null;
        try {
            rpcRef = RefFactory.Invoke(this, context, isBackend).RequireServer();

            // Runs before GetServerPeer, before AcceptWebSocketAsync and before any Disconnect,
            // so a request that fails the proof can't evict the incumbent connection, can't create
            // a peer, and can't obtain an upgraded socket. It must also stay above the unsupported
            // format branch below, which accepts the socket just to send close code 4001.
            if (!TryVerifyReconnectProof(context, rpcRef)) {
                Log.LogWarning("'{PeerRef}': Rejected RPC connection: invalid reconnect proof for {Request}",
                    rpcRef, requestDescription);
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            if (Hub.SerializationFormats.GetClientRejectionReason(rpcRef.SerializationFormat) is { } rejectionReason) {
                Log.LogWarning(
                    "'{PeerRef}': Rejected {Request}: {Reason}",
                    rpcRef, requestDescription, rejectionReason);
#if NET6_0_OR_GREATER
                var rejectAcceptContext = Options.ConfigureWebSocket.Invoke(this, context, rpcRef);
                webSocket = await context.WebSockets.AcceptWebSocketAsync(rejectAcceptContext).ConfigureAwait(false);
#else
                webSocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
#endif
                await webSocket.CloseAsync(
                    (WebSocketCloseStatus)RpcWebSocketCloseCode.UnsupportedFormat,
                    $"Unsupported RPC serialization format: '{rpcRef.SerializationFormat}'",
                    cancellationToken
                    ).ConfigureAwait(false);
                return;
            }

            Log.LogInformation("'{PeerRef}': Accepting RPC connection for {Request}", rpcRef, requestDescription);
            var peer = Hub.GetServerPeer(rpcRef);

            // Disconnect any stale connection BEFORE upgrading the new WebSocket.
            // Doing this after AcceptWebSocketAsync would consume the client's HandshakeTimeout,
            // because old-connection teardown can take up to RpcWebSocketTransport.Options.CloseTimeout
            // on a dead socket; performing it before the upgrade consumes ConnectTimeout instead,
            // which is the correct budget for "waiting for server to be ready to talk".
            // Use IsConnectedOrHandshaking, not IsConnected: a client reconnecting faster than
            // its previous handshake completes would otherwise stack new connections against a
            // peer stuck mid-handshake instead of replacing the stale one.
            if (peer.ConnectionState.Value.IsConnectingOrConnected()) {
                Log.LogWarning("'{PeerRef}': {Peer} is already connected, disconnecting the old connection first...",
                    rpcRef, peer);
                await peer.Disconnect(cancellationToken).ConfigureAwait(false);
            }

#if NET6_0_OR_GREATER
            var webSocketAcceptContext = Options.ConfigureWebSocket.Invoke(this, context, rpcRef);
            var acceptWebSocketTask = context.WebSockets.AcceptWebSocketAsync(webSocketAcceptContext);
#else
            var acceptWebSocketTask = context.WebSockets.AcceptWebSocketAsync();
#endif
            webSocket = await acceptWebSocketTask.ConfigureAwait(false);
            var properties = PropertyBag.Empty
                .KeylessSet((RpcPeer)peer)
                .KeylessSet(context)
                .KeylessSet(webSocket);
            var webSocketOwner = new WebSocketOwner(peer.Route.ToString(), webSocket, Services);
            var transportOptions = WebSocketClientOptions.WebSocketTransportOptionsFactory.Invoke(peer, properties);
            var stopTokenSource = cancellationToken.CreateLinkedTokenSource();
            var transport = new RpcWebSocketTransport(transportOptions, peer, webSocketOwner, stopTokenSource) {
                OwnsWebSocketOwner = false,
            };
            connection = await PeerOptions.ServerConnectionFactory
                .Invoke(peer, transport, properties, cancellationToken)
                .ConfigureAwait(false);

            await peer.SetNextConnection(connection, cancellationToken).ConfigureAwait(false);
            await transport.WhenClosed.ConfigureAwait(false);
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
}
