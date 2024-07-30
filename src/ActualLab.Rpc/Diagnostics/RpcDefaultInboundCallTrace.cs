using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public sealed class RpcDefaultInboundCallTrace(RpcDefaultCallTracer tracer, Activity? activity)
    : RpcInboundCallTrace(activity)
{
    public override void Complete(RpcInboundCall call, double durationMs)
    {
        if (Activity != null) {
            Activity.Finalize(call.UntypedResultTask, call.CancellationToken);
            Activity.Dispose();
        }
        if (!tracer.InboundCallCounter.Enabled)
            return;

        tracer.InboundCallCounter.Add(1);
        var resultTask = call.UntypedResultTask;
        if (!resultTask.IsCompletedSuccessfully())
            (resultTask.IsCanceled ? tracer.InboundCancellationCounter : tracer.InboundErrorCounter).Add(1);
        tracer.InboundDurationHistogram.Record(durationMs);
    }
}
