using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

/// <summary>
/// Abstract base class representing a trace for an inbound RPC call with an associated activity.
/// </summary>
public abstract class RpcInboundCallTrace(Activity? activity)
{
    public readonly Activity? Activity = activity;

    public abstract void Complete(RpcInboundCall call);
}
