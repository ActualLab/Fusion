using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Middlewares;

public record struct RpcMiddlewareOutput<T>(IRpcMiddleware? Middleware, Func<RpcInboundCall, Task<T>> Invoker);
