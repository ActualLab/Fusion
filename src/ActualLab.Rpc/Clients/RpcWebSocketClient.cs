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
        if (uri is null) {
            // The expected behavior for null URI is to wait indefinitely
            Log.LogWarning("'{PeerRef}': No connection URL - waiting for peer termination", clientPeer.Ref);
            await TaskExt.NeverEnding(cancellationToken).ConfigureAwait(false);
        }

        // Log.LogInformation("'{PeerRef}': Connection URL: {Url}", clientPeer.Ref, uri);
        var hub = clientPeer.Hub;
        using var connectCts = cancellationToken.CreateLinkedTokenSource(hub.Limits.ConnectTimeout);
        var connectToken = connectCts.Token;
        WebSocketOwner webSocketOwner;
        try {
            webSocketOwner = Options.WebSocketOwnerFactory.Invoke(clientPeer);
            try {
                await webSocketOwner.ConnectAsync(uri!, connectToken).ConfigureAwait(false);
            }
            catch {
                await webSocketOwner.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        catch (Exception e) {
            if (e.IsCancellationOfTimeoutToken(connectToken, cancellationToken))
                throw Errors.ConnectTimeout();

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
