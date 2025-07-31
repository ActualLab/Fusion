using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable CA1721

public class RpcInboundContext
{
    private static readonly AsyncLocal<RpcInboundContext?> CurrentLocal = new();

    public static RpcInboundContext? Current {
        get => CurrentLocal.Value;
        set => CurrentLocal.Value = value;
    }

    public readonly RpcPeer Peer;
    public readonly RpcMessage Message;
    public readonly CancellationToken PeerChangedToken;
    public readonly CpuTimestamp CreatedAt = CpuTimestamp.Now;
    public RpcInboundCall Call { get; protected init; }

    public static RpcInboundContext GetCurrent()
        => CurrentLocal.Value ?? throw Errors.NoCurrentRpcInboundContext();

    public RpcInboundContext(RpcPeer peer, RpcMessage message, CancellationToken peerChangedToken)
        : this(peer, message, peerChangedToken, true)
    { }

#pragma warning disable CA1068
    protected RpcInboundContext(RpcPeer peer, RpcMessage message, CancellationToken peerChangedToken, bool initializeCall)
#pragma warning restore CA1068
    {
        Peer = peer;
        Message = message;
        PeerChangedToken = peerChangedToken;
        Call = initializeCall ? RpcInboundCall.New(message.CallTypeId, this, GetMethodDef()) : null!;
    }

    // Nested types

    private RpcMethodDef? GetMethodDef()
    {
        var methodRef = Message.MethodRef;
        var method = methodRef.Target ?? Peer.ServerMethodResolver[methodRef];
        if (method is null || method.IsSystem)
            return method;

        return Peer.InboundCallFilter.Invoke(Peer, method) ? method : null;
    }
}
