using ActualLab.Rpc.Internal;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc.Clients;

/// <summary>
/// An <see cref="RpcClient"/> implementation that establishes connections via WebSockets.
/// </summary>
public class RpcWebSocketClient(IServiceProvider services) : RpcClient(services)
{
    public RpcWebSocketClientOptions Options { get; } = services.GetRequiredService<RpcWebSocketClientOptions>();

    public override Task<RpcConnection> ConnectRemote(RpcClientPeer clientPeer, CancellationToken cancellationToken)
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
        WebSocketOwner webSocketOwner;
        try {
            webSocketOwner = await Task
                .Run(async () => {
                    WebSocketOwner? o = null;
                    try {
                        o = Options.WebSocketOwnerFactory.Invoke(clientPeer);
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

            // If we're here, the connection was established successfully.
            // On some platforms / .NET versions, ClientWebSocket.ConnectAsync may retain a CancellationToken
            // registration on the underlying socket; if connectToken fires after connect completes,
            // it can abort the already-established socket, causing SocketError 125 (ECANCELED) on ReceiveAsync.
            // ReSharper disable once AccessToModifiedClosure
            connectTokenSource.DisposeSilently();
        }
        catch (Exception e) {
            if (e.IsCancellationOf(connectToken) && !cancellationToken.IsCancellationRequested)
                throw Errors.ConnectTimeout();

            Log.LogWarning(e, "'{PeerRef}': Failed to connect to {Url}", clientPeer.Ref, uri);
            throw;
        }
        finally {
            connectTokenSource.CancelAndDisposeSilently();
        }

        var properties = PropertyBag.Empty
            .KeylessSet((RpcPeer)clientPeer)
            .KeylessSet(uri)
            .KeylessSet(webSocketOwner)
            .KeylessSet(webSocketOwner.WebSocket);
        var transportOptions = Options.WebSocketTransportOptionsFactory(clientPeer, properties);
        var transport = new RpcWebSocketTransport(transportOptions, clientPeer, webSocketOwner);
        return new RpcConnection(transport, properties);
    }
}
