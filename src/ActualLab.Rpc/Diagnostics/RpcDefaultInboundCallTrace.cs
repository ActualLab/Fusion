using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

/// <summary>
/// Default inbound call trace that finalizes the activity and records call metrics on completion.
/// </summary>
public sealed class RpcDefaultInboundCallTrace(RpcDefaultCallTracer tracer, Activity? activity)
    : RpcInboundCallTrace(activity)
{
    public override void Complete(RpcInboundCall call)
    {
        if (Activity is not null) {
            var untypedResultTask = call.ResultTask;
            if (untypedResultTask is null) {
                StaticLog.For(typeof(RpcDefaultInboundCallTrace)).LogError("Call doesn't have ResultTask yet");
                untypedResultTask = Task.CompletedTask;
            }

            Activity.Finalize(untypedResultTask, call.CallCancelToken);
            Activity.DisposeNonCurrent();
        }

        var isTracerEnabled = tracer.IsEnabled;
        var areInstrumentsEnabled = RpcInstruments.IsEnabled;
        if (!isTracerEnabled && !areInstrumentsEnabled)
            return;

        var callStats = new RpcCallSummary(call);
        if (isTracerEnabled)
            tracer.RegisterInboundCall(callStats);
        if (areInstrumentsEnabled)
            RpcInstruments.RegisterInboundCall(callStats);
    }
}
