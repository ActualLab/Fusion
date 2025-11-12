using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable CA1721

public sealed class RpcInboundContext
{
    private static readonly AsyncLocal<RpcInboundContext?> CurrentLocal = new();

    public static RpcInboundContext? Current {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CurrentLocal.Value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CurrentLocal.Value = value;
    }

    public readonly RpcPeer Peer;
    public readonly RpcMessage Message;
    public readonly CancellationToken PeerChangedToken;
    public readonly CpuTimestamp CreatedAt = CpuTimestamp.Now;
    public readonly RpcInboundCall Call;

    public static RpcInboundContext GetCurrent()
        => CurrentLocal.Value ?? throw Errors.NoCurrentRpcInboundContext();

    public RpcInboundContext(RpcPeer peer, RpcMessage message, CancellationToken peerChangedToken)
    {
        Peer = peer;
        Message = message;
        PeerChangedToken = peerChangedToken;
        Call = RpcInboundCall.New(message.CallTypeId, this, GetMethodDef());
    }

    // Nested types

    private RpcMethodDef? GetMethodDef()
    {
        var methodRef = Message.MethodRef;
        var method = methodRef.Target ?? Peer.ServerMethodResolver[methodRef];
        if (method is null || method.IsSystem || method.InboundCallFilter is not { } filter)
            return method;

        return filter.Invoke(Peer) ? method : null;
    }
}
