using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

[StructLayout(LayoutKind.Auto)]
public readonly record struct RpcCallSummary(
    TaskResultKind ResultKind,
    double DurationMs)
{
    public RpcCallSummary(RpcInboundCall inboundCall)
        : this(
            inboundCall.UntypedResultTask?.GetResultKind() ?? TaskResultKind.Incomplete,
            inboundCall.Context.CreatedAt.Elapsed.TotalMilliseconds)
    { }
}
