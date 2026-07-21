using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc.Clients;

/// <summary>
/// An <see cref="RpcClient"/> implementation that establishes connections via WebSockets.
/// </summary>
public class RpcWebSocketClient(IServiceProvider services) : RpcClient(services)
{
    public RpcWebSocketClientOptions Options { get; } = services.GetRequiredService<RpcWebSocketClientOptions>();

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
        var connectTokenSource = (CancellationTokenSource?)new CancellationTokenSource();
        var connectToken = connectTokenSource!.Token;
        _ = hub.SystemClock
            .Delay(hub.Limits.ConnectTimeout, cancellationToken)
            .ContinueWith(_ => connectTokenSource.CancelAndDisposeSilently(), TaskScheduler.Default);
        WebSocketOwner webSocketOwner;
        WebSocketOwner? pendingWebSocketOwner = null;
        var mustDisposePendingOwner = true;
        try {
            // Hold a reference outside Task.Run so we can dispose the WebSocketOwner from
            // the outer scope if the inner ConnectAsync hangs past connectToken cancellation.
            // On some platforms (notably Browser/WASM), ConnectAsync may not honor cancellation,
            // leaving the inner task orphaned with a ClientWebSocket stuck in CONNECTING state.
            webSocketOwner = await Task
                .Run(async () => {
                    var o = Options.WebSocketOwnerFactory.Invoke(clientPeer);
                    // ReSharper disable once AccessToModifiedClosure
                    Volatile.Write(ref pendingWebSocketOwner, o);
                    await o.ConnectAsync(uri!, connectToken).ConfigureAwait(false);
                    return o;
                }, connectToken)
                .WaitAsync(connectToken) // MAUI sometimes stucks in the sync part of ConnectAsync
                .ConfigureAwait(false);

            // Success: the owner is now owned by the returned RpcConnection, so the outer finally must not dispose it
            mustDisposePendingOwner = false;
            // On some platforms / .NET versions, ClientWebSocket.ConnectAsync may retain a CancellationToken
            // registration on the underlying socket; if connectToken fires after connect completes,
            // it can abort the already-established socket, causing SocketError 125 (ECANCELED) on ReceiveAsync.
            connectTokenSource.DisposeSilently();
            connectTokenSource = null;
        }
        catch (Exception e) {
            if (e.IsCancellationOf(connectToken) && !cancellationToken.IsCancellationRequested)
                throw Errors.ConnectTimeout();

            Log.LogWarning(e, "'{Route}': Failed to connect to {Url}", clientPeer.Route, uri);
            throw;
        }
        finally {
            connectTokenSource.CancelAndDisposeSilently();
            if (mustDisposePendingOwner && Volatile.Read(ref pendingWebSocketOwner) is { } orphanedOwner) {
                // The inner Task.Run is either still hung in ConnectAsync (and won't reach us) or has produced
                // an owner we won't return. Disposing the WebSocketOwner releases the underlying WebSocket,
                // which typically also unblocks the orphaned ConnectAsync.
                // We fire-and-forget so the outer caller doesn't wait on a potentially-also-hung Dispose.
                _ = Task.Run(async () => {
                    try {
                        await orphanedOwner.DisposeAsync().ConfigureAwait(false);
                    }
                    catch {
                        // Intended
                    }
                }, CancellationToken.None);
            }
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
