using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

/// <summary>
/// Default outbound call trace that finalizes the activity on completion.
/// </summary>
public sealed class RpcDefaultOutboundCallTrace(Activity? activity)
    : RpcOutboundCallTrace(activity)
{
    public override void Complete(RpcOutboundCall call)
    {
        if (Activity is null)
            return;

        Activity.Finalize(call.ResultTask);
        Activity.DisposeNonCurrent();
    }
}
