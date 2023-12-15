using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public abstract class RpcClient : RpcServiceBase
{
    public string ClientId { get; init; }

    protected RpcClient(IServiceProvider services)
        : base(services)
        => ClientId = Hub.ClientIdGenerator.Invoke();

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract Task<RpcConnection> CreateConnection(RpcClientPeer peer, CancellationToken cancellationToken);
}
