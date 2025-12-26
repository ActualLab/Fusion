using System.Net.WebSockets;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc.Clients;

public class RpcWebSocketClient(IServiceProvider services)
    : RpcClient(services)
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
        Log.LogDebug("Peer '{PeerRef}' (ClientId={Id}) connecting to '{Url}'", clientPeer.Ref, clientPeer.ClientId, uri);
        if (uri is null) {
            // The expected behavior for null URI is to wait indefinitely
            Log.LogWarning("'{PeerRef}': No connection URL - waiting for peer termination", clientPeer.Ref);
            await TaskExt.NeverEnding(cancellationToken).ConfigureAwait(false);
        }

        // Log.LogInformation("'{PeerRef}': Connection URL: {Url}", clientPeer.Ref, uri);
        var hub = clientPeer.Hub;
        var connectCts = new CancellationTokenSource();
        var connectToken = connectCts.Token;
        _ = hub.Clock
            // ReSharper disable once PossiblyMistakenUseOfCancellationToken
            .Delay(hub.Limits.ConnectTimeout, cancellationToken)
            .ContinueWith(_ => connectCts.CancelAndDisposeSilently(), TaskScheduler.Default);
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
        }
        catch (Exception e) {
            if (e.IsCancellationOf(connectToken) && !cancellationToken.IsCancellationRequested)
                throw Errors.ConnectTimeout();

            Log.LogWarning(e, "Peer '{PeerRef}' failed to connect to '{Url}'", clientPeer.Ref, uri);
            throw;
        }

        var properties = PropertyBag.Empty
            .KeylessSet((RpcPeer)clientPeer)
            .KeylessSet(uri)
            .KeylessSet(webSocketOwner)
            .KeylessSet(webSocketOwner.WebSocket);
        var webSocketChannelOptions = Options.WebSocketChannelOptionsFactory(clientPeer, properties);
        var channel = new WebSocketChannel<RpcMessage>(webSocketChannelOptions, webSocketOwner);
        return new RpcConnection(channel, properties);
    }
}
