using System.Diagnostics.CodeAnalysis;
using ActualLab.Caching;
using ActualLab.OS;

namespace ActualLab.Async;

public static partial class TaskExt
{
    private static readonly ConcurrentDictionary<Type, Func<object?, Task>> FromResultFactoryCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<Type, Task> FromDefaultResultCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    // FromResult

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume Task<T> constructors are preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume Task<T> constructors are preserved")]
    public static Task FromResult(object? result, Type resultType)
        => FromResultFactoryCache.GetOrAdd(resultType,
            static t => {
                // ReSharper disable once UseCollectionExpression
                var taskType = typeof(Task<>).MakeGenericType(t);
                var ctor = taskType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [t], null);
                return (Func<object?, Task>)ActivatorExt.CreateConstructorDelegate(ctor, typeof(object))!;
            }).Invoke(result);

    // FromDefaultResult

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume Task<T> constructors are preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume Task<T> constructors are preserved")]
    public static Task FromDefaultResult(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicParameterlessConstructor |
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type resultType)
        => FromDefaultResultCache.GetOrAdd(resultType,
            static t => {
                // ReSharper disable once UseCollectionExpression
                var taskType = typeof(Task<>).MakeGenericType(t);
                var ctor = taskType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null, [t], null);
                return (Task)ctor!.Invoke([t.GetDefaultValue()]);
            });

    // FromException

    public static Task FromException(Exception exception, Type resultType)
        => GenericInstanceCache
            .Get<Func<Exception, Task>>(typeof(TaskFromExceptionFactory<>), resultType)
            .Invoke(exception);

    // ToUntypedValueTask

    public static ValueTask<object?> ToUntypedValueTask(this Task task, Type resultType)
        => GenericInstanceCache
            .Get<Func<Task, ValueTask<object?>>>(typeof(ToUntypedValueTaskFactory<>), resultType)
            .Invoke(task);

    // ToTypedXxx

    public static object ToTypedValueTask(this Task task, Type resultType)
        => GenericInstanceCache
            .Get<Func<Task, object>>(typeof(ToTypedValueTaskFactory<>), resultType)
            .Invoke(task);

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "FromTypedTaskInternal is preserved")]
    public static IResult ToTypedResultSynchronously(this Task task, Type resultType)
        => GenericInstanceCache
            .Get<Func<Task, IResult>>(typeof(ToTypedResultSynchronouslyFactory<>), resultType)
            .Invoke(task);

    public static Task<IResult> ToTypedResultAsync(this Task task, Type resultType)
        => task.ContinueWith(
            t => t.ToTypedResultSynchronously(resultType),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    // Nested types

    public sealed class TaskFromExceptionFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume Task<T> methods are preserved")]
        public override Func<Exception, Task> Generate()
            => typeof(T) == typeof(ValueVoid)
                ? Task.FromException
                : Task.FromException<T>;
    }

    public sealed class ToTypedValueTaskFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override Func<Task, object> Generate()
            => typeof(T) == typeof(ValueVoid)
                ? static source => new ValueTask(source)
                : static source => new ValueTask<T>((Task<T>)source);
    }

    public sealed class ToUntypedValueTaskFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override Func<Task, ValueTask<object?>> Generate()
            => typeof(T) == typeof(ValueVoid)
                ? static async source => {
                    await source.ConfigureAwait(false);
                    return null;
                }
                : static async source => await ((Task<T>)source).ConfigureAwait(false);
    }

    public sealed class ToTypedResultSynchronouslyFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        private static IResult ConvertVoid(Task task)
            // ReSharper disable once HeapView.BoxingAllocation
            => task.IsCompletedSuccessfully()
                ? new Result<Unit>()
                : new Result<Unit>(default, task.AssertCompleted().GetBaseException());

        private static IResult Convert(Task task)
            // ReSharper disable once HeapView.BoxingAllocation
            => task.IsCompletedSuccessfully()
                ? new Result<T>(((Task<T>)task).Result)
                : new Result<T>(default!, task.AssertCompleted().GetBaseException());

        public override Func<Task, IResult> Generate()
            => typeof(T) == typeof(ValueVoid) ? ConvertVoid : Convert;
    }
}
