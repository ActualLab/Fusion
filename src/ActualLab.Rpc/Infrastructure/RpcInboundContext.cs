using ActualLab.Interception;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable CA1721

/// <summary>
/// Encapsulates the context for processing an inbound RPC message on a peer.
/// </summary>
public sealed class RpcInboundContext
{
    private static readonly AsyncLocal<RpcInboundContext?> CurrentLocal = new();

    public static RpcInboundContext? Current {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => CurrentLocal.Value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] set => CurrentLocal.Value = value;
    }

    public readonly RpcPeer Peer;
    public readonly RpcInboundMessage Message;
    public readonly CancellationToken PeerChangedToken;
    public readonly CpuTimestamp CreatedAt = CpuTimestamp.Now;
    public readonly RpcMethodDef? MethodDef;
    public readonly RpcInboundCall Call;
    public object? RelatedObject; // IRpcPolymorphicArgumentHandler and the actual call handler may use this field

    public static RpcInboundContext GetCurrent()
        => CurrentLocal.Value ?? throw Errors.NoCurrentRpcInboundContext();

    public RpcInboundContext(RpcPeer peer, RpcInboundMessage message, CancellationToken peerChangedToken)
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
        if (MethodDef.IsBackend && !Peer.Ref.IsBackend) {
            MethodDef = Peer.Hub.SystemCallSender.NotFoundMethodDef;
            var (service, method) = message.MethodRef.GetServiceAndMethodName();
            Call = new RpcInboundNotFoundCall<Unit>(this) {
                Arguments = ArgumentList.New(service, method)
            };
            return;
        }

        // The method's required call type is always built; a downgrade is signaled via
        // Message.CallTypeId, and the method's own call type is what decides whether it's one
        // this method understands.
        var isDowngrade = MethodDef.CallType.Id != message.CallTypeId;
        if (isDowngrade && !MethodDef.CallType.DowngradeValidator.Invoke(message.CallTypeId)) {
            MethodDef = Peer.Hub.SystemCallSender.NotFoundMethodDef;
            var (service, method) = message.MethodRef.GetServiceAndMethodName();
            Call = new RpcInboundInvalidCallTypeCall<Unit>(this, MethodDef.CallType.Id, message.CallTypeId) {
                // This prevents argument deserialization
                Arguments = ArgumentList.New(service, method)
            };
            return;
        }

        // A downgrade normally keeps the method's own inbound call type - a compute method invoked
        // as Regular is still an RpcInboundComputeCall, which handles that via IsRegularCall.
        // Invalidate is the exception: it does something else entirely, so it needs its own.
        Call = message.CallTypeId == RpcCallTypeIds.Invalidate
            ? RpcInboundCall.GetFactory(MethodDef, RpcCallTypeIds.Invalidate).Invoke(this)
            : MethodDef.InboundCallFactory.Invoke(this);
    }
}
