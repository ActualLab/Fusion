using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Middlewares;

/// <summary>
/// Carries the <see cref="IRpcMiddleware"/> pipeline state for a specific <see cref="RpcMethodDef"/>.
/// </summary>
public sealed class RpcMiddlewareContext<T>
{
    public readonly RpcMethodDef MethodDef;
    public readonly List<IRpcMiddleware> RemainingMiddlewares;
    public readonly List<RpcMiddlewareOutput<T>> Outputs;
    public ILogger Log => field ??= MethodDef.Hub.Services.LogFor(GetType());

    public RpcMiddlewareContext(RpcMethodDef methodDef)
    {
        MethodDef = methodDef;
        RemainingMiddlewares = MethodDef.Hub.Middlewares.ToList();
        if (MethodDef.MiddlewareFilter is { } middlewareFilter)
            RemainingMiddlewares.RemoveAll(x => !middlewareFilter.Invoke(x));
        // NativeAOT bug: this downcast triggers FailFast (exit code 3) instead of succeeding.
        // The actual runtime type IS Func<RpcInboundCall, Task<T>>, so the cast is valid.
        // We use Unsafe.As in Release to work around this.
#if DEBUG
        var inboundCallInvoker = (Func<RpcInboundCall, Task<T>>)MethodDef.InboundCallInvoker;
#else
        var inboundCallInvoker = Unsafe.As<Func<RpcInboundCall, Task<T>>>(MethodDef.InboundCallInvoker);
#endif
        var firstOutput = new RpcMiddlewareOutput<T>(Middleware: null, inboundCallInvoker);
        Outputs = [firstOutput];
    }
}
