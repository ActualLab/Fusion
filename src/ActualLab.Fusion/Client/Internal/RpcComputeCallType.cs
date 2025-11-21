using ActualLab.Rpc;

namespace ActualLab.Fusion.Client.Internal;

public static class RpcComputeCallType
{
    public static readonly byte Id = 1;

    public static void Register()
        => RpcCallTypeRegistry.Register(new(Id) {
            InboundCallType = typeof(RpcInboundComputeCall<>),
            OutboundCallType = typeof(RpcOutboundComputeCall<>),
            InboundCallTypeOverridesInvokeServer = true,
        });
}
