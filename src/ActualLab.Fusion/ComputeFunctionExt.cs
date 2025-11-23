using System.Diagnostics.CodeAnalysis;
using ActualLab.Caching;

namespace ActualLab.Fusion;

public static class ComputeFunctionExt
{
    private static readonly Type FactoryType1 = typeof(CompleteProduceValuePromiseFactory<>);
    private static readonly Type FactoryType2 = typeof(CompleteProduceValuePromiseWithSynchronizerFactory<>);

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume ComputeFunctionExt<T> methods are preserved")]
    public static Task ProduceValuePromise(
        this IComputeFunction function,
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        var task = function.ProduceComputed(input, context, cancellationToken);
        if (task.IsCompletedSuccessfully()) {
            var computed = task.GetAwaiter().GetResult();
            return computed.GetValuePromise(); // Happy path
        }

        return GenericInstanceCache
            .GetUnsafe<Func<Task<Computed>, Task>>(FactoryType1, function.OutputType)
            .Invoke(task);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume ComputeFunctionExt<T> methods are preserved")]
    public static Task ProduceValuePromise(
        this IComputeFunction function,
        ComputedInput input,
        ComputeContext context,
        ComputedSynchronizer computedSynchronizer,
        CancellationToken cancellationToken = default)
    {
        var task = function.ProduceComputed(input, context, cancellationToken);
        if (task.IsCompletedSuccessfully()) {
            var computed = task.GetAwaiter().GetResult();
            if (computedSynchronizer.IsSynchronized(computed))
                return computed.GetValuePromise(); // Happy path
        }

        return GenericInstanceCache
            .GetUnsafe<Func<Task<Computed>, ComputedSynchronizer, CancellationToken, Task>>(FactoryType2, function.OutputType)
            .Invoke(task, computedSynchronizer, cancellationToken);
    }

    // Nested types

    public sealed class CompleteProduceValuePromiseFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume Task<T> methods are preserved")]
        public override object Generate()
            => static (Task<Computed> computedTask) => {
                var resultTask = computedTask.ContinueWith(
                    static t => {
                        var computed = t.GetAwaiter().GetResult();
                        return (T)computed.UntypedOutput.Value!;
                    },
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                return resultTask;
            };
    }

    public sealed class CompleteProduceValuePromiseWithSynchronizerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume Task<T> methods are preserved")]
        public override object Generate()
            => static (Task<Computed> computedTask, ComputedSynchronizer computedSynchronizer, CancellationToken cancellationToken) => {
                var resultTask = computedSynchronizer.Synchronize(computedTask, cancellationToken).ContinueWith(
                    static t => {
                        var computed = t.GetAwaiter().GetResult();
                        return (T)computed.UntypedOutput.Value!;
                    },
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                return resultTask;
            };
    }
}
