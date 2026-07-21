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
    public RpcCallSummary(RpcInboundCall inboundCall, Exception? completionError = null)
        : this(
            completionError is null
                ? inboundCall.ResultTask?.GetResultKind() ?? TaskResultKind.Incomplete
                : completionError is OperationCanceledException
                    ? TaskResultKind.Cancellation
                    : TaskResultKind.Error,
            inboundCall.Context.CreatedAt.Elapsed.TotalMilliseconds)
    { }
}
