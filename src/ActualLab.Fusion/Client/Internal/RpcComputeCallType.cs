using ActualLab.Rpc;

namespace ActualLab.Fusion.Client.Internal;

/// <summary>
/// Registers the Fusion-specific RPC call type for compute calls.
/// </summary>
public static class RpcComputeCallType
{
    public const byte Id = 1;
    public static readonly RpcCallType Value;

    static RpcComputeCallType()
    {
        Value = new RpcCallType(Id) {
            InboundCallType = typeof(RpcInboundComputeCall<>),
            OutboundCallType = typeof(RpcOutboundComputeCall<>),
        };
        RpcCallTypes.Register(Value);
    }
}
