using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Testing;

public record RpcRandomDelayInboundMiddleware : IRpcInboundMiddleware
{
    public RandomTimeSpan Delay { get; init; } = new(0.05, 0.03);
    public Func<RpcInboundCall, TimeSpan>? DelayProvider { get; init; }

    public Func<RpcInboundCall, Task>? CreatePreprocessor(RpcMethodDef methodDef)
        => call => {
            var delay = DelayProvider is { } delayProvider
                ? delayProvider.Invoke(call)
                : Delay.Next();
            return Task.Delay(delay, call.CallCancelToken);
        };

    public Func<RpcInboundCall, Task>? CreatePostprocessor(RpcMethodDef methodDef)
        => null;
}
