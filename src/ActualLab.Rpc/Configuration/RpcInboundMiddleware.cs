using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Middlewares;

namespace ActualLab.Rpc;

public interface IRpcMiddleware
{
    public double Priority { get; }

    public Func<RpcInboundCall, Task<T>> Create<T>(
        RpcMiddlewareContext<T> context,
        Func<RpcInboundCall, Task<T>> next);
}
