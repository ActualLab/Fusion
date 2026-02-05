using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Middlewares;

/// <summary>
/// Pairs an <see cref="IRpcMiddleware"/> with its resulting invoker delegate in the middleware pipeline.
/// </summary>
public record struct RpcMiddlewareOutput<T>(IRpcMiddleware? Middleware, Func<RpcInboundCall, Task<T>> Invoker);
