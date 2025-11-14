using ActualLab.Caching;
using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public partial class RpcMethodDef
{
    public sealed class InboundCallServerInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => (RpcMethodDef methodDef) => {
                if (!methodDef.IsAsyncMethod)
                    throw new ArgumentOutOfRangeException(nameof(methodDef), "Async method is required here.");

                if (methodDef.IsSystem && methodDef.Service.Type == typeof(IRpcSystemCalls)) {
                    // "Handcrafted" invokers for the most frequent system calls
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

    public sealed class InboundCallPipelineInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => (RpcMethodDef methodDef) => {
                if (!methodDef.IsAsyncMethod)
                    throw new ArgumentOutOfRangeException(nameof(methodDef), "Async method is required here.");

                var preprocessors = methodDef.InboundCallPreprocessors.Length != 0
                    ? methodDef.InboundCallPreprocessors
                    : null;
                var validator = methodDef.InboundCallValidator;

                return methodDef.IsAsyncVoidMethod
                    ? (Func<RpcInboundCall, Task<T>>)(async call => {
                        // Task (returns Task<Unit>)
                        if (preprocessors is not null)
                            foreach (var p in preprocessors)
                                await p.Invoke(call).ConfigureAwait(false);
                        validator?.Invoke(call);
                        await call.InvokeServer().ConfigureAwait(false);
                        return default!;
                    })
                    : async call => {
                        // Task<T>
                        if (preprocessors is not null)
                            foreach (var p in preprocessors)
                                await p.Invoke(call).ConfigureAwait(false);
                        validator?.Invoke(call);
                        return await ((Task<T>)call.InvokeServer()).ConfigureAwait(false);
                    };
            };
    }

    public sealed class InboundCallPipelineFastInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => (RpcMethodDef methodDef) => {
                if (!methodDef.IsAsyncMethod)
                    throw new ArgumentOutOfRangeException(nameof(methodDef), "Async method is required here.");

                object? server = null;
                var invoker = methodDef.ArgumentListInvoker;
                var preprocessors = methodDef.InboundCallPreprocessors.Length != 0
                    ? methodDef.InboundCallPreprocessors
                    : null;
                var validator = methodDef.InboundCallValidator;

                return (Func<RpcInboundCall, Task<T>>)((methodDef.ReturnsTask, methodDef.IsAsyncVoidMethod) switch {
                    (true, true) => async call => {
                        // Task (returns Task<Unit>)
                        if (preprocessors is not null)
                            foreach (var p in preprocessors)
                                await p.Invoke(call).ConfigureAwait(false);
                        server ??= methodDef.Service.Server!;
                        await ((Task)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                        return default!;
                    },
                    (true, false) => async call => {
                        // Task<T>
                        if (preprocessors is not null)
                            foreach (var p in preprocessors)
                                await p.Invoke(call).ConfigureAwait(false);
                        validator?.Invoke(call);
                        server ??= methodDef.Service.Server!;
                        return await ((Task<T>)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                    },
                    (false, true) => async call => {
                        // ValueTask (returns Task<Unit>)
                        if (preprocessors is not null)
                            foreach (var p in preprocessors)
                                await p.Invoke(call).ConfigureAwait(false);
                        validator?.Invoke(call);
                        server ??= methodDef.Service.Server!;
                        await ((ValueTask)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                        return default!;
                    },
                    (false, false) => async call => {
                        // ValueTask<T> (returns Task<T>)
                        if (preprocessors is not null)
                            foreach (var p in preprocessors)
                                await p.Invoke(call).ConfigureAwait(false);
                        validator?.Invoke(call);
                        server ??= methodDef.Service.Server!;
                        return await ((ValueTask<T>)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                    },
                });
            };
    }
}
