using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public abstract class RpcClient(IServiceProvider services) : RpcServiceBase(services)
{
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract Task<RpcConnection> Connect(RpcClientPeer peer, CancellationToken cancellationToken);
}
