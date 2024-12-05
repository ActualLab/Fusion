using ActualLab.Channels;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public abstract class RpcClient(IServiceProvider services) : RpcServiceBase(services)
{
    public BoundedChannelOptions LocalChannelOptions { get; init; } = new(256) {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = true,
    };

    public Task<RpcConnection> Connect(RpcClientPeer clientPeer, CancellationToken cancellationToken)
        => clientPeer.ConnectionKind switch {
            RpcPeerConnectionKind.Remote => ConnectRemote(clientPeer, cancellationToken),
            RpcPeerConnectionKind.Loopback => ConnectLoopback(clientPeer, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(clientPeer),
                $"Invalid {nameof(clientPeer)}.{nameof(clientPeer.ConnectionKind)} value: {clientPeer.ConnectionKind}"),
        };

    public abstract Task<RpcConnection> ConnectRemote(RpcClientPeer clientPeer, CancellationToken cancellationToken);

    public virtual Task<RpcConnection> ConnectLoopback(RpcClientPeer clientPeer, CancellationToken cancellationToken)
    {
        var serverPeerRef = RpcPeerRef.NewServer(
            RpcPeerRef.LoopbackKeyPrefix + clientPeer.ClientId,
            clientPeer.SerializationFormat.Key,
            clientPeer.Ref.IsBackend);
        var serverPeer = Hub.GetServerPeer(serverPeerRef);
        var channelPair = ChannelPair.CreateTwisted<RpcMessage>(LocalChannelOptions);
        var clientConnection = new RpcConnection(channelPair.Channel1, PropertyBag.Empty.Set((RpcPeer)clientPeer)) {
            IsLocal = true,
        };
        var serverConnection = new RpcConnection(channelPair.Channel2, PropertyBag.Empty.Set((RpcPeer)serverPeer)) {
            IsLocal = true,
        };
        serverPeer.SetConnection(serverConnection);
        return Task.FromResult(clientConnection);
    }
}
