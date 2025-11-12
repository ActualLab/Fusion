using ActualLab.Caching;
using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public partial class RpcMethodDef
{
    public Func<RpcPeer, bool>? InboundCallFilter { get; init; } = null;
    public Func<RpcInboundCall, Task>[] InboundCallPreprocessors { get; init; } = [];
    public Action<RpcInboundCall>? InboundCallValidator { get; init; } = null;

    // The delegates and properties below must be initialized in Initialize(),
    // they are supposed to be as efficient as possible (i.e., do less, if possible)
    // taking the values of other properties into account.
    public Func<ArgumentList, Task> InboundCallServerInvoker { get; protected set; } = null!;
    public Func<RpcInboundCall, Task> InboundCallPipelineInvoker { get; protected set; } = null!;

    public virtual Func<RpcPeer, bool>? CreateInboundCallFilter()
        => IsBackend
            ? peer => peer.Ref.IsBackend
            : null;

    public virtual Func<RpcInboundCall, Task>[] CreateInboundCallPreprocessors()
        => Hub.InboundCallPreprocessors
            .Select(x => x.CreateInboundCallPreprocessor(this))
            .ToArray();

    public virtual Action<RpcInboundCall>? CreateInboundCallValidator()
    {
#if NET6_0_OR_GREATER // NullabilityInfoContext is available in .NET 6.0+
        if (IsSystem || NoWait)
            return null; // These methods are supposed to rely on built-in validation for perf. reasons

        var nonNullableArgIndexesList = new List<int>();
        var nullabilityInfoContext = new NullabilityInfoContext();
        for (var i = 0; i < Parameters.Length; i++) {
            var p = Parameters[i];
            if (p.ParameterType.IsClass && nullabilityInfoContext.Create(p).ReadState == NullabilityState.NotNull)
                nonNullableArgIndexesList.Add(i);
        }
        if (nonNullableArgIndexesList.Count == 0)
            return null;

        var nonNullableArgIndexes = nonNullableArgIndexesList.ToArray();
        return call => {
            var args = call.Arguments!;
            foreach (var index in nonNullableArgIndexes)
                ArgumentNullException.ThrowIfNull(args.GetUntyped(index), Parameters[index].Name);
        };
#else
        return null;
#endif
    }

    // Nested types

    public sealed class InboundCallServerInvokerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => (RpcMethodDef methodDef) => {
                if (!methodDef.IsAsyncMethod)
                    throw new ArgumentOutOfRangeException(nameof(methodDef), "Async method is required here.");

                var server = methodDef.Service.Server!;
                var invoker = methodDef.ArgumentListInvoker;

                return (Func<ArgumentList, Task<T>>)((methodDef.ReturnsTask, methodDef.IsAsyncVoidMethod) switch {
                    (true, true) => async args => { // Task (returns Task<Unit>)
                        await ((Task)invoker.Invoke(server, args)!).ConfigureAwait(false);
                        return default!;
                    },
                    (true, false) => async args => { // Task<T>
                        // ReSharper disable once ConvertToLambdaExpression
                        return await ((Task<T>)invoker.Invoke(server, args)!).ConfigureAwait(false);
                    },
                    (false, true) => async args => { // ValueTask (returns Task<Unit>)
                        await ((ValueTask)invoker.Invoke(server, args)!).ConfigureAwait(false);
                        return default!;
                    },
                    (false, false) => async args => { // ValueTask<T> (returns Task<T>)
                        // ReSharper disable once ConvertToLambdaExpression
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

                var server = methodDef.Service.Server!;
                var invoker = methodDef.ArgumentListInvoker;
                var preprocessors = methodDef.InboundCallPreprocessors.Length != 0
                    ? methodDef.InboundCallPreprocessors
                    : null;
                var validator = methodDef.InboundCallValidator;

                return (Func<RpcInboundCall, Task<T>>)((methodDef.ReturnsTask, methodDef.IsAsyncVoidMethod) switch {
                    (true, true) => async call => { // Task (returns Task<Unit>)
                        if (preprocessors is not null)
                            foreach (var p in preprocessors)
                                await p.Invoke(call).ConfigureAwait(false);
                        validator?.Invoke(call);
                        await ((Task)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                        return default!;
                    },
                    (true, false) => async call => { // Task<T>
                        if (preprocessors is not null)
                            foreach (var p in preprocessors)
                                await p.Invoke(call).ConfigureAwait(false);
                        validator?.Invoke(call);
                        return await ((Task<T>)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                    },
                    (false, true) => async call => { // ValueTask (returns Task<Unit>)
                        if (preprocessors is not null)
                            foreach (var p in preprocessors)
                                await p.Invoke(call).ConfigureAwait(false);
                        validator?.Invoke(call);
                        await ((ValueTask)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                        return default!;
                    },
                    (false, false) => async call => { // ValueTask<T> (returns Task<T>)
                        if (preprocessors is not null)
                            foreach (var p in preprocessors)
                                await p.Invoke(call).ConfigureAwait(false);
                        validator?.Invoke(call);
                        return await ((ValueTask<T>)invoker.Invoke(server, call.Arguments!)!).ConfigureAwait(false);
                    },
                });
            };
    }
}
