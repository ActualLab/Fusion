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
        // This downcast triggers assertion/FailFast (exit code 3) in NativeAOT, which is a bug.
        // We workaround it with unsafe cast, which is fully safe here.
        // var inboundCallInvoker = (Func<RpcInboundCall, Task<T>>)MethodDef.InboundCallInvoker;
        var inboundCallInvoker = Unsafe.As<Func<RpcInboundCall, Task<T>>>(MethodDef.InboundCallInvoker);
        var firstOutput = new RpcMiddlewareOutput<T>(Middleware: null, inboundCallInvoker);
        Outputs = [firstOutput];
    }
}
