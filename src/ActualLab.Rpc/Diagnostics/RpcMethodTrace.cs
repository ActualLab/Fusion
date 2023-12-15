using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public abstract class RpcMethodTrace
{
    public abstract void OnResultTaskReady(RpcInboundCall call);
}
