using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public abstract class RpcCallTracer(RpcMethodDef method)
{
    public readonly RpcMethodDef Method = method;

    public abstract RpcInboundCallTrace? StartInboundTrace(RpcInboundCall call);
    public abstract RpcOutboundCallTrace? StartOutboundTrace(RpcOutboundCall call);
}
