using ActualLab.Rpc;

namespace ActualLab.Fusion.Client.Internal;

/// <summary>
/// Registers the Fusion-specific RPC call type for compute calls.
/// </summary>
public static class RpcComputeCallType
{
    public const byte Id = RpcCallTypeIds.Compute;
    public static readonly RpcCallType Value;

    static RpcComputeCallType()
    {
        Value = new RpcCallType(Id) {
            InboundCallType = typeof(RpcInboundComputeCall<>),
            OutboundCallType = typeof(RpcOutboundComputeCall<>),
            DowngradeValidator = static callTypeId
                => callTypeId is RpcCallTypeIds.Regular or RpcCallTypeIds.Invalidate,
        };
        RpcCallTypes.Register(Value);
    }
}
