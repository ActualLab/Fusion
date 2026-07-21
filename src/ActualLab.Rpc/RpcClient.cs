using ActualLab.Channels;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

/// <summary>
/// Abstract base class responsible for establishing RPC connections to remote peers.
/// </summary>
public abstract class RpcClient(IServiceProvider services) : RpcServiceBase(services)
{
    public BoundedChannelOptions LocalChannelOptions { get; init; } = new(256) {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = true,
    };

    public Task<RpcConnection> Connect(
        RpcClientPeer clientPeer,
        RpcPeerConnectionState connectionState,
        CancellationToken cancellationToken)
        => clientPeer.ConnectionKind switch {
            RpcPeerConnectionKind.Remote => ConnectRemote(clientPeer, connectionState, cancellationToken),
            RpcPeerConnectionKind.Loopback => ConnectLoopback(clientPeer, connectionState, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(clientPeer),
                $"Invalid {nameof(clientPeer)}.{nameof(clientPeer.ConnectionKind)} value: {clientPeer.ConnectionKind}"),
        };

    public abstract Task<RpcConnection> ConnectRemote(
        RpcClientPeer clientPeer,
        RpcPeerConnectionState connectionState,
        CancellationToken cancellationToken);

    public virtual async Task<RpcConnection> ConnectLoopback(
        RpcClientPeer clientPeer,
        RpcPeerConnectionState connectionState,
        CancellationToken cancellationToken)
    {
        var serverPeerRef = RpcRef.NewServer(
            clientPeer.ClientId,
            clientPeer.SerializationFormat.Key,
            clientPeer.Ref.IsBackend,
            RpcPeerConnectionKind.Loopback);
        var serverPeer = Hub.GetServerPeer(serverPeerRef);
        var channelPair = ChannelPair.CreateTwisted<ArrayOwner<byte>>(LocalChannelOptions);

        var clientTransport = new RpcSimpleChannelTransport(clientPeer, channelPair.Channel1);
        var clientConnection = new RpcConnection(clientTransport, PropertyBag.Empty.KeylessSet((RpcPeer)clientPeer)) {
            IsLocal = true,
        };

        var serverTransport = new RpcSimpleChannelTransport(serverPeer, channelPair.Channel2);
        var serverConnection = new RpcConnection(serverTransport, PropertyBag.Empty.KeylessSet((RpcPeer)serverPeer)) {
            IsLocal = true,
        };

        await serverPeer.SetNextConnection(serverConnection, cancellationToken).ConfigureAwait(false);
        return clientConnection;
    }

    public virtual void OnConnectionStateChange(RpcClientPeer clientPeer, RpcPeerConnectionState connectionState)
    { }
}
