using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public sealed class RpcDefaultCallTrace(RpcDefaultCallTracer tracer, Activity? activity) : RpcCallTrace
{
    public override void Complete(RpcInboundCall call, double durationMs)
    {
        activity?.Dispose();
        if (tracer.CallCounter.IsObservable) {
            tracer.CallCounter.Add(1);
            var resultTask = call.UntypedResultTask;
            if (!resultTask.IsCompletedSuccessfully())
                (resultTask.IsCanceled ? tracer.CancellationCounter : tracer.ErrorCounter).Add(1);
            tracer.DurationHistogram.Record(durationMs);
        }
    }
}
