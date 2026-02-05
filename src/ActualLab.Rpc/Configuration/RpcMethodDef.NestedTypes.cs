using ActualLab.Caching;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Middlewares;

namespace ActualLab.Rpc;

public partial class RpcMethodDef
{
    /// <summary>
    /// Factory that creates server-side invoker delegates for inbound RPC calls.
    /// </summary>
    public sealed class InboundCallServerInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => (RpcMethodDef methodDef) => {
                if (!methodDef.IsAsyncMethod)
                    throw new ArgumentOutOfRangeException(nameof(methodDef), "Async method is required here.");

                if (methodDef.IsSystem && methodDef.Service.Type == typeof(IRpcSystemCalls)) {
                    // "Handcrafted" invokers for the most frequent system calls.
                    // Note that NoWait calls (such as 'Ok') typically return Task or Task<RpcNoWait>
                    // instead of Task<T>, where T is UnwrapResultType, so the assumption is that
                    // invokers for these calls
                    var systemCalls = (RpcSystemCalls)methodDef.Service.Server!;
                    switch (methodDef.SystemMethodKind) {
                    case RpcSystemMethodKind.Ok:
                        return (Func<RpcInboundCall, Task>)(call => systemCalls.Ok(call.Arguments!.Get0Untyped()));
                    case RpcSystemMethodKind.Match:
                        return (Func<RpcInboundCall, Task>)(_ => systemCalls.M());
                    case RpcSystemMethodKind.Item:
                        return (Func<RpcInboundCall, Task>)(call => {
                            var args = call.Arguments!;
                            return systemCalls.I((long)args.Get0Untyped()!, args.Get1Untyped());
                        });
                    case RpcSystemMethodKind.Batch:
                        return (Func<RpcInboundCall, Task>)(call => {
                            var args = call.Arguments!;
                            return systemCalls.B((long)args.Get0Untyped()!, args.Get1Untyped());
                        });
                    case RpcSystemMethodKind.NotFound:
                        return (Func<RpcInboundCall, Task>)(call => ((IRpcInboundNotFoundCall)call).InvokeImpl());
                    }
                }

                object? server = null;
                var invoker = methodDef.ArgumentListInvoker;
                return (Func<RpcInboundCall, Task<T>>)((methodDef.ReturnsTask, methodDef.IsAsyncVoidMethod) switch {
                    (true, true) => async call => {
                        // Task (returns Task<Unit>)
                        server ??= methodDef.Service.Server!;
                        await ((Task)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                        return default!;
                    },
                    (true, false) => async call => {
                        // Task<T>
                        server ??= methodDef.Service.Server!;
                        return await ((Task<T>)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                    },
                    (false, true) => async call => {
                        // ValueTask (returns Task<Unit>)
                        server ??= methodDef.Service.Server!;
                        await ((ValueTask)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                        return default!;
                    },
                    (false, false) => async call => {
                        // ValueTask<T> (returns Task<T>)
                        server ??= methodDef.Service.Server!;
                        return await ((ValueTask<T>)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                    },
                });
            };
    }

    /// <summary>
    /// Factory that wraps the server invoker with the middleware pipeline for inbound RPC calls.
    /// </summary>
    public sealed class InboundCallMiddlewareInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => (RpcMethodDef methodDef) => {
                var context = new RpcMiddlewareContext<T>(methodDef);
                while (context.RemainingMiddlewares.Count > 0) {
                    var lastMiddlewareIndex = context.RemainingMiddlewares.Count - 1;
                    var middleware = context.RemainingMiddlewares[lastMiddlewareIndex];
                    context.RemainingMiddlewares.RemoveAt(lastMiddlewareIndex);
                    var prevInvoker = context.Outputs[^1].Invoker;
                    var invoker = middleware.Create(context, prevInvoker);
                    context.Outputs.Add(new(middleware, invoker));
                }
                return context.Outputs[^1].Invoker; // Last middleware's output
            };
    }
}
