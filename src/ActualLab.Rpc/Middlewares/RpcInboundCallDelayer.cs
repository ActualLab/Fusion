using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Middlewares;

public sealed record RpcInboundCallDelayer : IRpcMiddleware
{
    public static Func<RpcMethodDef, bool> DefaultFilter { get; set; } = _ => true;

    public double Priority { get; init; } = RpcInboundMiddlewarePriority.Initial;
    public Func<RpcMethodDef, bool> Filter { get; init; } = DefaultFilter;
    public RandomTimeSpan Delay { get; init; } = new(0.05, 0.03);
    public Func<RpcInboundCall, TimeSpan>? DelayProvider { get; init; }

    public Func<RpcInboundCall, Task<T>> Create<T>(RpcMiddlewareContext<T> context, Func<RpcInboundCall, Task<T>> next)
    {
        if (!Filter.Invoke(context.MethodDef))
            return next;

        return call => {
            var delay = DelayProvider is { } delayProvider
                ? delayProvider.Invoke(call)
                : Delay.Next();
            return delay > TimeSpan.Zero
                ? CompleteAsync(call, delay, next)
                : next.Invoke(call); // No delay -> we don't want to spawn an extra task
        };
    }

    private static async Task<T> CompleteAsync<T>(
        RpcInboundCall call, TimeSpan delay, Func<RpcInboundCall, Task<T>> next)
    {
        await Task.Delay(delay, call.CallCancelToken).ConfigureAwait(false);
        return await next.Invoke(call).ConfigureAwait(false);
    }
}
