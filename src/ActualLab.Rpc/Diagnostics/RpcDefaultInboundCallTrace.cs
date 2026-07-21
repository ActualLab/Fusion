using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

/// <summary>
/// Default inbound call trace that finalizes the activity and records call metrics on completion.
/// </summary>
public sealed class RpcDefaultInboundCallTrace(RpcDefaultCallTracer tracer, Activity? activity)
    : RpcInboundCallTrace(activity)
{
    public override void Complete(RpcInboundCall call, Exception? error)
    {
        // ResultTask may be null or incomplete here (aborted / never-processed calls) -
        // RpcCallSummary maps that state to TaskResultKind.Incomplete
        if (call.ResultTask is { IsCompleted: true } resultTask)
            error ??= resultTask.ToResultSynchronously().Error;
        if (Activity is not null) {
            Activity.Finalize(error, call.CallCancelToken);
            Activity.DisposeNonCurrent();
        }
        if (!tracer.IsEnabled)
            return;

        var callStats = new RpcCallSummary(call, error);
        tracer.RegisterInboundCall(callStats, error);
    }
}
