using System.Net;
using System.Net.WebSockets;
using Microsoft.Owin;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.WebSockets;
using WebSocketAccept = System.Action<
    System.Collections.Generic.IDictionary<string, object>, // WebSocket Accept parameters
    System.Func< // WebSocketFunc callback
        System.Collections.Generic.IDictionary<string, object>, // WebSocket environment
        System.Threading.Tasks.Task>>;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Server-side handler that accepts incoming WebSocket connections and establishes
/// RPC peer connections for OWIN-based .NET Framework hosts.
/// </summary>
public class RpcWebSocketServer(RpcWebSocketServerOptions settings, IServiceProvider services)
    : RpcServiceBase(services)
{
    private const string OriginHeaderName = "Origin";

    public RpcWebSocketServerOptions Settings { get; } = settings;
    public RpcPeerOptions PeerOptions { get; } = services.GetRequiredService<RpcPeerOptions>();
    public RpcWebSocketClientOptions WebSocketClientOptions { get; } = services.GetRequiredService<RpcWebSocketClientOptions>();
    public RpcWebSocketServerRefFactory RefFactory { get; } = services.GetRequiredService<RpcWebSocketServerRefFactory>();

    public virtual async Task<HttpStatusCode> Invoke(IOwinContext context, bool isBackend)
    {
        // Based on https://stackoverflow.com/questions/41848095/websockets-using-owin

        var acceptToken = context.Get<WebSocketAccept>("websocket.Accept");
        if (acceptToken is null)
            return HttpStatusCode.BadRequest;

        // Runs before RefFactory, so a rejected request creates no server peer.
        // OWIN has no equivalent of WebSocketOptions.AllowedOrigins, so this is the only origin gate here.
        var origin = context.Request.Headers.Get(OriginHeaderName) ?? "";
        if (!Settings.OriginValidator.Invoke(this, context, origin)) {
            Log.LogWarning("Rejected RPC connection from origin '{Origin}' for {Path}{Query}",
                origin, context.Request.Path, RpcQuerySanitizer.Sanitize(context.Request.QueryString.Value));
            return HttpStatusCode.Forbidden;
        }

        var rpcRef = RefFactory.Invoke(this, context, isBackend).RequireServer();

        // Runs before GetServerPeer, before the WebSocket upgrade and before any Disconnect,
        // so a request that fails the proof can't evict the incumbent connection
        // and can't create a peer. See RpcWebSocketServer.Invoke in ActualLab.Rpc.Server.
        if (!TryVerifyReconnectProof(context, rpcRef)) {
            Log.LogWarning("'{PeerRef}': Rejected RPC connection - invalid reconnect proof for {Path}{Query}",
                rpcRef, context.Request.Path, RpcQuerySanitizer.Sanitize(context.Request.QueryString.Value));
            return HttpStatusCode.Forbidden;
        }

        // Validate serialization format before peer creation to avoid KeyNotFoundException
        if (Hub.SerializationFormats.GetClientRejectionReason(rpcRef.SerializationFormat) is { } rejectionReason) {
            Log.LogWarning("'{PeerRef}': Rejected the connection: {Reason}", rpcRef, rejectionReason);
            return HttpStatusCode.BadRequest;
        }

        var peer = Hub.GetServerPeer(rpcRef);

        // Disconnect any stale connection BEFORE upgrading the new WebSocket.
        // Doing this after the upgrade would consume the client's HandshakeTimeout,
        // because old-connection teardown can take up to RpcWebSocketTransport.Options.CloseTimeout
        // on a dead socket; performing it before the upgrade consumes ConnectTimeout instead,
        // which is the correct budget for "waiting for server to be ready to talk".
        // Use IsConnectedOrHandshaking, not IsConnected: a client reconnecting faster than
        // its previous handshake completes would otherwise stack new connections against a
        // peer stuck mid-handshake instead of replacing the stale one.
        if (peer.ConnectionState.Value.IsConnectingOrConnected()) {
            Log.LogWarning("'{PeerRef}': {Peer} is already connected, disconnecting the old connection first...",
                rpcRef, peer);
            try {
                await peer.Disconnect(context.Request.CallCancelled).ConfigureAwait(false);
            }
            catch (Exception e) when (!e.IsCancellationOf(context.Request.CallCancelled)) {
                Log.LogWarning(e, "'{PeerRef}': Failed to disconnect old connection", rpcRef);
                return HttpStatusCode.InternalServerError;
            }
        }

        var acceptOptions = Settings.ConfigureWebSocket.Invoke(this, context, rpcRef);
        acceptToken(acceptOptions, wsEnv => {
            var wsContext = (WebSocketContext)wsEnv["System.Net.WebSockets.WebSocketContext"];
            return HandleWebSocket(context, wsContext, rpcRef);
        });

        return HttpStatusCode.SwitchingProtocols;
    }

    // Protected methods

    protected virtual bool TryVerifyReconnectProof(IOwinContext context, RpcRef rpcRef)
    {
        var query = context.Request.Query;
        var counterText = query[Settings.ReconnectProofCounterParameterName] ?? "";
        var proof = query[Settings.ReconnectProofParameterName] ?? "";
        // rpcRef.HostInfo is the raw clientId, so the value in the HMAC is the one that selected the peer
        Hub.TryGetServerPeer(rpcRef, out var peer);
        return RpcReconnectProof.TryVerify(
            peer, rpcRef.HostInfo, counterText, proof, Settings.RequireReconnectProof);
    }

    // Private methods

    private async Task HandleWebSocket(IOwinContext context, WebSocketContext wsContext, RpcRef rpcRef)
    {
        var cancellationToken = context.Request.CallCancelled;
        WebSocket? webSocket = null;
        WebSocketOwner? webSocketOwner = null;
        RpcConnection? connection = null;
        try {
            var peer = Hub.GetServerPeer(rpcRef);

            webSocket = wsContext.WebSocket;
            var properties = PropertyBag.Empty
                .KeylessSet((RpcPeer)peer)
                .KeylessSet(context)
                .KeylessSet(webSocket);
            webSocketOwner = new WebSocketOwner(peer.Route.ToString(), webSocket, Services);
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
            if (connection is not null || e.IsCancellationOf(cancellationToken))
                return; // Intended: this is typically a normal connection termination

            var request = context.Request;
            Log.LogWarning(e, "Failed to accept RPC connection: {Path}{Query}",
                request.Path, RpcQuerySanitizer.Sanitize(request.QueryString.Value));
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
            if (webSocketOwner is not null)
                await webSocketOwner.DisposeAsync().ConfigureAwait(false);
            else
                webSocket?.Dispose();
        }
    }
}
