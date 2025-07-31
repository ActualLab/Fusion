using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

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
