using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public abstract class RpcCallTrace
{
    public abstract void Complete(RpcInboundCall call, double durationMs);
}
