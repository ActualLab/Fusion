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
            return task.Result.GetValuePromise();

        return GenericInstanceCache
            .Get<Func<Task<Computed>, Task>>(typeof(CompleteProduceValuePromiseFactory<>), function.OutputType)
            .Invoke(task);
    }

    // Nested types

    public sealed class CompleteProduceValuePromiseFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume Task<T> methods are preserved")]
        public override object Generate()
            => (Task<Computed> task) => task.ContinueWith(
                static t => {
                    var computed = t.GetAwaiter().GetResult();
                    return (T)computed.Output.Value!;
                },
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
