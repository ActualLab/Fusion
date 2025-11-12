using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public interface IRpcInboundCallPreprocessor
{
    protected static readonly Func<RpcInboundCall, Task> None = static _ => Task.CompletedTask;

    public Func<RpcInboundCall, Task> CreateInboundCallPreprocessor(RpcMethodDef methodDef);
}
