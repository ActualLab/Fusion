using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

public enum RpcObjectKind
{
    Local = 0,
    Remote,
}

public interface IRpcObject : IHasId<RpcObjectId>
{
    public RpcObjectKind Kind { get; }
    public Task Reconnect(CancellationToken cancellationToken);
    public void Disconnect();
}

public interface IRpcSharedObject : IRpcObject
{
    public bool IsReconnectable { get; }
    public Moment LastKeepAliveAt { get; }
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
