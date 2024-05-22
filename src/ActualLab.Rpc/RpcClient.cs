using System.Diagnostics.CodeAnalysis;
using ActualLab.Channels;
using ActualLab.Internal;
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

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract Task<RpcConnection> Connect(RpcClientPeer clientPeer, CancellationToken cancellationToken);

    protected virtual Task<RpcConnection> ConnectLocal(RpcClientPeer clientPeer, CancellationToken cancellationToken)
    {
        var serverPeerRef = RpcPeerRef.NewServer(clientPeer.ClientId, clientPeer.Ref.IsBackend);
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
