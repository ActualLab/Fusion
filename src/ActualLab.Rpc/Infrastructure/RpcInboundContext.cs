using ActualLab.Interception;
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
    public readonly RpcMethodDef? MethodDef;
    public readonly RpcInboundCall Call;
    public object? RelatedObject; // IRpcPolymorphicArgumentHandler and the actual call handler may use this field

    public static RpcInboundContext GetCurrent()
        => CurrentLocal.Value ?? throw Errors.NoCurrentRpcInboundContext();

    public RpcInboundContext(RpcPeer peer, RpcMessage message, CancellationToken peerChangedToken)
    {
        Peer = peer;
        Message = message;
        PeerChangedToken = peerChangedToken;
        var methodRef = Message.MethodRef;
        MethodDef = methodRef.Target ?? Peer.ServerMethodResolver[methodRef];
        if (MethodDef is null) {
            MethodDef = Peer.Hub.SystemCallSender.NotFoundMethodDef;
            var (service, method) = message.MethodRef.GetServiceAndMethodName();
            Call = new RpcInboundNotFoundCall<Unit>(this) {
                // This prevents argument deserialization
                Arguments = ArgumentList.New(service, method)
            };
            return;
        }

        if (MethodDef.CallType.Id != message.CallTypeId) {
            MethodDef = Peer.Hub.SystemCallSender.NotFoundMethodDef;
            var (service, method) = message.MethodRef.GetServiceAndMethodName();
            Call = new RpcInboundInvalidCallTypeCall<Unit>(this, MethodDef.CallType.Id, message.CallTypeId) {
                // This prevents argument deserialization
                Arguments = ArgumentList.New(service, method)
            };
            return;
        }

        Call = MethodDef.InboundCallFactory.Invoke(this);
    }
}
