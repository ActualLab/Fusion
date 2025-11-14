using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public interface IRpcInboundCallPreprocessor
{
    public Func<RpcInboundCall, Task> CreateInboundCallPreprocessor(RpcMethodDef methodDef);
}

public abstract class RpcInboundCallPreprocessor : IRpcInboundCallPreprocessor
{
    public static readonly Func<RpcInboundCall, Task> None = static _ => Task.CompletedTask;

    public abstract Func<RpcInboundCall, Task> CreateInboundCallPreprocessor(RpcMethodDef methodDef);
}
