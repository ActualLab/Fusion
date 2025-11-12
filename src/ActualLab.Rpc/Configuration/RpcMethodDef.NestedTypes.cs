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

                object? server = null;
                var invoker = methodDef.ArgumentListInvoker;

                return (Func<ArgumentList, Task<T>>)((methodDef.ReturnsTask, methodDef.IsAsyncVoidMethod) switch {
                    (true, true) => async args => {
                        // Task (returns Task<Unit>)
                        server ??= methodDef.Service.Server!;
                        await ((Task)invoker.Invoke(server, args)!).ConfigureAwait(false);
                        return default!;
                    },
                    (true, false) => async args => {
                        // Task<T>
                        server ??= methodDef.Service.Server!;
                        return await ((Task<T>)invoker.Invoke(server, args)!).ConfigureAwait(false);
                    },
                    (false, true) => async args => {
                        // ValueTask (returns Task<Unit>)
                        server ??= methodDef.Service.Server!;
                        await ((ValueTask)invoker.Invoke(server, args)!).ConfigureAwait(false);
                        return default!;
                    },
                    (false, false) => async args => {
                        // ValueTask<T> (returns Task<T>)
                        server ??= methodDef.Service.Server!;
                        return await ((ValueTask<T>)invoker.Invoke(server, args)!).ConfigureAwait(false);
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

                // var server = methodDef.Service.Server!;
                // var invoker = methodDef.ArgumentListInvoker;
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
                        validator?.Invoke(call);
                        // await ((Task)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                        await call.InvokeServer().ConfigureAwait(false);
                        return default!;
                    },
                    (true, false) => async call => {
                        // Task<T>
                        if (preprocessors is not null)
                            foreach (var p in preprocessors)
                                await p.Invoke(call).ConfigureAwait(false);
                        validator?.Invoke(call);
                        // return await ((Task<T>)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                        return await ((Task<T>)call.InvokeServer()).ConfigureAwait(false);
                    },
                    (false, true) => async call => {
                        // ValueTask (returns Task<Unit>)
                        if (preprocessors is not null)
                            foreach (var p in preprocessors)
                                await p.Invoke(call).ConfigureAwait(false);
                        validator?.Invoke(call);
                        // await ((ValueTask)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                        await call.InvokeServer().ConfigureAwait(false);
                        return default!;
                    },
                    (false, false) => async call => {
                        // ValueTask<T> (returns Task<T>)
                        if (preprocessors is not null)
                            foreach (var p in preprocessors)
                                await p.Invoke(call).ConfigureAwait(false);
                        validator?.Invoke(call);
                        // return await ((ValueTask<T>)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                        return await ((Task<T>)call.InvokeServer()).ConfigureAwait(false);
                    },
                });
            };
    }
}
