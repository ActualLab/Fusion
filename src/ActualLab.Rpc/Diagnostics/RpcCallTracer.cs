using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

/// <summary>
/// Abstract base class for creating inbound and outbound call traces for an RPC method.
/// </summary>
public abstract class RpcCallTracer(RpcMethodDef methodDef)
{
    public readonly RpcMethodDef MethodDef = methodDef;

    public abstract RpcInboundCallTrace? StartInboundTrace(RpcInboundCall call);
    public abstract RpcOutboundCallTrace? StartOutboundTrace(RpcOutboundCall call);
}
