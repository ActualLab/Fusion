using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public abstract class RpcCallTracer(RpcMethodDef method)
{
    public readonly RpcMethodDef Method = method;
    public Sampler Sampler { get; init; } = Sampler.Always;

    public abstract RpcCallTrace? TryStartTrace(RpcInboundCall call);
}
