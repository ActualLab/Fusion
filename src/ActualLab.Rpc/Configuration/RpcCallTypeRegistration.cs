using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public sealed record RpcCallTypeRegistration(byte Id)
{
    public Type InboundCallType { get; init; } = typeof(RpcInboundCall<>);
    public Type OutboundCallType { get; init; } = typeof(RpcOutboundCall<>);
    public bool InboundCallTypeOverridesInvokeServer { get; init; } = true;

    public override string ToString()
        => $"{Id}: {InboundCallType.GetName()} / {OutboundCallType.GetName()}";
}
