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
        if (Activity is not null) {
            var untypedResultTask = call.ResultTask;
            if (untypedResultTask is null) {
                StaticLog.For(typeof(RpcDefaultInboundCallTrace)).LogError("Call doesn't have ResultTask yet");
                untypedResultTask = Task.CompletedTask;
            }

            error ??= untypedResultTask.ToResultSynchronously().Error;
            Activity.Finalize(error, call.CallCancelToken);
            Activity.DisposeNonCurrent();
        }

        if (!tracer.IsEnabled)
            return;

        error ??= call.ResultTask?.ToResultSynchronously().Error;
        var callStats = new RpcCallSummary(call, error);
        tracer.RegisterInboundCall(callStats, error);
    }
}
