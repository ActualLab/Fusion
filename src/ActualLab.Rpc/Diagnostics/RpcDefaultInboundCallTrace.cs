using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public sealed class RpcDefaultInboundCallTrace(RpcDefaultCallTracer tracer, Activity? activity)
    : RpcInboundCallTrace(activity)
{
    public override void Complete(RpcInboundCall call)
    {
        if (Activity != null) {
            Activity.Finalize(call.UntypedResultTask, call.CancellationToken);
            Activity.Dispose();
        }

        var callStats = new RpcCallSummary(call);
        if (tracer.InboundCallCounter.Enabled)
            tracer.RegisterInboundCall(callStats);
        if (RpcMeters.InboundCallCounter.Enabled)
            RpcMeters.RegisterInboundCall(callStats);
    }
}
