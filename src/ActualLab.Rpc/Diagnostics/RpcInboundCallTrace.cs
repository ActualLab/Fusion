using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public abstract class RpcInboundCallTrace(Activity? activity)
{
    public readonly Activity? Activity = activity;

    public abstract void Complete(RpcInboundCall call, double durationMs);
}
