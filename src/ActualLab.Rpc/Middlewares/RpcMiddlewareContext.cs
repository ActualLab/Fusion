using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Middlewares;

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
        var firstOutput = new RpcMiddlewareOutput<T>(null, (Func<RpcInboundCall, Task<T>>)MethodDef.InboundCallInvoker);
        Outputs = [firstOutput];
    }
}
