using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public abstract class RpcMethodTracer(RpcMethodDef method)
{
    public RpcMethodDef Method { get; init; } = method;
    public Sampler Sampler { get; init; } = Sampler.Always;

    public abstract RpcMethodTrace? TryStartTrace(RpcInboundCall call);
}
