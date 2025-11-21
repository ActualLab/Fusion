using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public interface IRpcInboundMiddleware
{
    public Func<RpcInboundCall, Task>? CreatePreprocessor(RpcMethodDef methodDef);
    public Func<RpcInboundCall, Task>? CreatePostprocessor(RpcMethodDef methodDef);
}
