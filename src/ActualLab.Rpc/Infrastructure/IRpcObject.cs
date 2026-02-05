using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Defines whether an RPC object is local or remote.
/// </summary>
public enum RpcObjectKind
{
    Local = 0,
    Remote,
}

/// <summary>
/// Represents an RPC object that can be reconnected or disconnected across peers.
/// </summary>
public interface IRpcObject : IHasId<RpcObjectId>
{
    public RpcObjectKind Kind { get; }
    public Task Reconnect(CancellationToken cancellationToken);
    public void Disconnect();
}

/// <summary>
/// An <see cref="IRpcObject"/> that is shared with a remote peer and supports keep-alive tracking.
/// </summary>
public interface IRpcSharedObject : IRpcObject
{
    public bool IsReconnectable { get; }
    public Moment LastKeepAliveAt { get; }
    public void KeepAlive();
}

/// <summary>
/// Extension methods for <see cref="IRpcObject"/>.
/// </summary>
public static class RpcObjectExt
{
    public static void RequireKind(this IRpcObject rpcObject, RpcObjectKind expectedKind)
    {
        if (rpcObject.Kind != expectedKind)
            throw Errors.InvalidRpcObjectKind(expectedKind);
    }
}
