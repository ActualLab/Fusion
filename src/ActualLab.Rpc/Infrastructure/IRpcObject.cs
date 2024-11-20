using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Internal;
using UnreferencedCode = ActualLab.Internal.UnreferencedCode;

namespace ActualLab.Rpc.Infrastructure;

public enum RpcObjectKind
{
    Local = 0,
    Remote,
}

public interface IRpcObject : IHasId<RpcObjectId>
{
    public RpcObjectKind Kind { get; }
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Reconnect(CancellationToken cancellationToken);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public void Disconnect();
}

public interface IRpcSharedObject : IRpcObject
{
    public CpuTimestamp LastKeepAliveAt { get; }
    public void KeepAlive();
}

public static class RpcObjectExt
{
    public static void RequireKind(this IRpcObject rpcObject, RpcObjectKind expectedKind)
    {
        if (rpcObject.Kind != expectedKind)
            throw Errors.InvalidRpcObjectKind(expectedKind);
    }
}
