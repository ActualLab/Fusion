using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

/// <summary>
/// Captures the result kind and duration of a completed RPC call for metrics recording.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct RpcCallSummary(
    TaskResultKind ResultKind,
    double DurationMs)
{
    public RpcCallSummary(RpcInboundCall inboundCall)
        : this(
            inboundCall.ResultTask?.GetResultKind() ?? TaskResultKind.Incomplete,
            inboundCall.Context.CreatedAt.Elapsed.TotalMilliseconds)
    { }
}
