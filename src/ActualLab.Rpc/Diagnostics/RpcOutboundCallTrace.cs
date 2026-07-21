using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

/// <summary>
/// Abstract base class representing a trace for an outbound RPC call with an associated activity.
/// </summary>
public abstract class RpcOutboundCallTrace(Activity? activity, ActivityContext parentActivityContext)
{
    public readonly Activity? Activity = activity;
    public readonly ActivityContext ParentActivityContext = parentActivityContext;
    public readonly ActivityContext ActivityContext = activity?.Context ?? parentActivityContext;
    public CpuTimestamp? MetricsStartedAt;

    public abstract void Complete(RpcOutboundCall call);

    internal void CompleteMetrics(RpcOutboundCall call)
    {
        if (MetricsStartedAt is not { } startedAt)
            return;

        var error = call.ResultTask.ToResultSynchronously().Error;
        if (error is RpcRerouteException)
            return;

        MetricsStartedAt = null;
        RpcInstruments.RegisterOutboundCall(call.MethodDef, startedAt.Elapsed.TotalMilliseconds, error);
    }
}
