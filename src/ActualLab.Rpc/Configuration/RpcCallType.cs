using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

/// <summary>
/// Identifies an RPC call type and its corresponding inbound/outbound call implementation types.
/// </summary>
public sealed record RpcCallType(byte Id)
{
    public Type InboundCallType { get; init; } = typeof(RpcInboundCall<>);
    public Type OutboundCallType { get; init; } = typeof(RpcOutboundCall<>);
    public Func<byte, bool> DowngradeValidator { get; init; } = static _ => false;

    public override string ToString()
        => $"{Id}: {InboundCallType.GetName()} / {OutboundCallType.GetName()}";
}
