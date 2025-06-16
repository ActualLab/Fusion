using System.Diagnostics.CodeAnalysis;
using ActualLab.Caching;

namespace ActualLab.Fusion;

public static class ComputeFunctionExt
{
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume ComputeFunctionExt<T> methods are preserved")]
    public static Task ProduceValuePromise(
        this IComputeFunction function,
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        var task = function.ProduceComputed(input, context, cancellationToken);
        if (task.IsCompletedSuccessfully())
            return task.Result.GetValuePromise(); // Happy path

        return GenericInstanceCache
            .Get<Func<Task<Computed>, Task>>(typeof(CompleteProduceValuePromiseFactory<>), function.OutputType)
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
            var computed = task.Result;
            if (computedSynchronizer.IsSynchronized(computed))
                return task.Result.GetValuePromise(); // Happy path
        }

        return GenericInstanceCache
            .Get<Func<Task<Computed>, ComputedSynchronizer, CancellationToken, Task>>(
                typeof(CompleteProduceValuePromiseWithSynchronizerFactory<>),
                function.OutputType
            ).Invoke(task, computedSynchronizer, cancellationToken);
    }

    // Nested types

    public sealed class CompleteProduceValuePromiseFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume Task<T> methods are preserved")]
        public override object Generate()
            => static (Task<Computed> computedTask) => computedTask.ContinueWith(
                static t => {
                    var computed = t.GetAwaiter().GetResult();
                    return (T)computed.Output.Value!;
                },
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    public sealed class CompleteProduceValuePromiseWithSynchronizerFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume Task<T> methods are preserved")]
        public override object Generate()
            => static async (Task<Computed> computedTask, ComputedSynchronizer computedSynchronizer, CancellationToken cancellationToken) => {
                var computed = await computedTask.ConfigureAwait(false);
                computed = await computed.Synchronize(computedSynchronizer, cancellationToken).ConfigureAwait(false);
                return (T)computed.Output.Value!;
            };
    }
}
