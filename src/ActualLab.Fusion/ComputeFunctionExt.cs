using System.Diagnostics.CodeAnalysis;
using ActualLab.OS;

namespace ActualLab.Fusion;

public static class ComputeFunctionExt
{
    private static readonly ConcurrentDictionary<Type, Func<Task<Computed>, Task>> CompleteProduceValuePromiseDelegateCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly MethodInfo CompleteProduceValuePromiseGenericMethod = typeof(ComputeFunctionExt)
        .GetMethod(nameof(CompleteProduceValuePromise), BindingFlags.Static | BindingFlags.NonPublic)!;

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume ComputeFunctionExt<T> methods are preserved")]
    public static Task ProduceValuePromise(
        this IComputeFunction function,
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken = default)
    {
        var task = function.ProduceComputed(input, context, cancellationToken);
        if (task.IsCompleted)
            return task.Result.GetValuePromise();

        return CompleteProduceValuePromiseDelegateCache.GetOrAdd(function.OutputType,
            static t => {
                var taskType = typeof(Task<>).MakeGenericType(t);
                var delegateType = typeof(Func<,>).MakeGenericType(typeof(Task<Computed>), taskType);
                var method = CompleteProduceValuePromiseGenericMethod.MakeGenericMethod(t);
                return (Func<Task<Computed>, Task>)method.CreateDelegate(delegateType);
            }).Invoke(task);
    }

    // Private methods

    private static Task<T> CompleteProduceValuePromise<T>(Task<Computed> task)
        => task.ContinueWith(
            static t => {
                var computed = t.GetAwaiter().GetResult();
                return (T)computed.Output.Value!;
            },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
