using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public abstract class RpcCallTracer(RpcMethodDef methodDef)
{
    public readonly RpcMethodDef MethodDef = methodDef;

    public abstract RpcInboundCallTrace? StartInboundTrace(RpcInboundCall call);
    public abstract RpcOutboundCallTrace? StartOutboundTrace(RpcOutboundCall call);
}
