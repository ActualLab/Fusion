using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public sealed class RpcDefaultInboundCallTrace(RpcDefaultCallTracer tracer, Activity? activity)
    : RpcInboundCallTrace(activity)
{
    public override void Complete(RpcInboundCall call)
    {
        if (Activity != null) {
            var untypedResultTask = call.UntypedResultTask;
            if (untypedResultTask == null) {
                StaticLog.For(typeof(RpcDefaultInboundCallTrace)).LogError("Call doesn't have ResultTask yet");
                untypedResultTask = Task.CompletedTask;
            }

            Activity.Finalize(untypedResultTask, call.CallCancelToken);
            Activity.DisposeNonCurrent();
        }

        var callStats = new RpcCallSummary(call);
        if (tracer.IsEnabled)
            tracer.RegisterInboundCall(callStats);
        if (RpcInstruments.IsEnabled)
            RpcInstruments.RegisterInboundCall(callStats);
    }
}
