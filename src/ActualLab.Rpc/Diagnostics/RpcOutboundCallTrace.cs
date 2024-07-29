using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public abstract class RpcOutboundCallTrace(Activity? activity)
{
    public readonly Activity? Activity = activity;

    public abstract void Complete(RpcOutboundCall call);
}
