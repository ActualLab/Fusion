using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Middlewares;

namespace ActualLab.Fusion.Tests.Services;

/// <summary>
/// An <see cref="IRpcMiddleware"/> counting inbound RPC calls to the methods matching its filter.
/// </summary>
public sealed record RpcInboundCallCounter : IRpcMiddleware
{
    private long _callCount;

    public double Priority { get; init; } = RpcInboundMiddlewarePriority.Initial;
    public Func<RpcMethodDef, bool> Filter { get; init; } = _ => true;
    public long CallCount => Interlocked.Read(ref _callCount);

    public Func<RpcInboundCall, Task<T>> Create<T>(
        RpcMiddlewareContext<T> context,
        Func<RpcInboundCall, Task<T>> next)
    {
        if (!Filter.Invoke(context.MethodDef))
            return next;

        return call => {
            Interlocked.Increment(ref _callCount);
            return next.Invoke(call);
        };
    }
}
