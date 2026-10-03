using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Internal;

/// <summary>
/// Registers the RPC call type that invalidates a compute method's result instead of producing it.
/// </summary>
public static class RpcInvalidateCallType
{
    public const byte Id = RpcCallTypeIds.Invalidate;
    public static readonly RpcCallType Value;

    static RpcInvalidateCallType()
    {
        Value = new RpcCallType(Id) {
            InboundCallType = typeof(RpcInboundInvalidateCall<>),
            // Nothing to track on the caller's side: this one behaves like a Regular call
            OutboundCallType = typeof(RpcOutboundCall<>),
        };
        RpcCallTypes.Register(Value);
    }
}
