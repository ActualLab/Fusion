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

    // ToUntypedXxx

    public static ValueTask<object?> ToUntypedValueTask(this Task task, Type resultType)
        => GenericInstanceCache
            .Get<Func<Task, ValueTask<object?>>>(typeof(ToUntypedValueTaskFactory<>), resultType)
            .Invoke(task);

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "FromTypedTaskInternal is preserved")]
    public static Result ToUntypedResultSynchronously(this Task task, Type resultType)
        => GenericInstanceCache
            .Get<Func<Task, Result>>(typeof(ToUntypedResultSynchronouslyFactory<>), resultType)
            .Invoke(task);

    public static Task<Result> ToUntypedResultAsync(this Task task, Type resultType)
        => task.ContinueWith(
            t => t.ToUntypedResultSynchronously(resultType),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    // Nested types

    public sealed class TaskFromExceptionFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume Task<T> methods are preserved")]
        public override object Generate()
            => typeof(T) == typeof(ValueVoid)
                ? (Func<Exception, Task>)Task.FromException
                : (Func<Exception, Task>)Task.FromException<T>;
    }

    public sealed class ToTypedValueTaskFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => typeof(T) == typeof(ValueVoid)
                ? static (Task source) => (object)new ValueTask(source)
                : static  (Task source) => (object)new ValueTask<T>((Task<T>)source);
    }

    public sealed class ToUntypedValueTaskFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => typeof(T) == typeof(ValueVoid)
                ? static (Task source) => {
                    if (source.IsCompletedSuccessfully())
                        return new ValueTask<object?>(null!);

                    var task = source.ContinueWith(
                        static t => {
                            t.GetAwaiter().GetResult();
                            return (object?)null;
                        },
                        CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    return new ValueTask<object?>(task);
                }
                : static (Task source) => {
                    if (source.IsCompletedSuccessfully())
                        return new ValueTask<object?>(((Task<T>)source).Result);

                    var task = source.ContinueWith(
                        static t => (object?)((Task<T>)t).GetAwaiter().GetResult(),
                        CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    return new ValueTask<object?>(task);
                };
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
                ? new Result<T>(((Task<T>)task).GetAwaiter().GetResult())
                : new Result<T>(default!, task.AssertCompleted().GetBaseException());

        public override object Generate()
            => typeof(T) == typeof(ValueVoid)
                ? (Func<Task, IResult>)ConvertVoid
                : (Func<Task, IResult>)Convert;
    }

    public sealed class ToUntypedResultSynchronouslyFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => typeof(T) == typeof(ValueVoid)
                ? static (Task source) => {
                    _ = source.AssertCompleted();
                    try {
                        source.GetAwaiter().GetResult();
                        return new Result(null, null);
                    }
                    catch (Exception e) {
                        return new Result(null, e);
                    }
                }
                : static (Task source) => {
                    _ = source.AssertCompleted();
                    try {
                        var result = ((Task<T>)source).GetAwaiter().GetResult();
                        return new Result(result, null);
                    }
                    catch (Exception e) {
                        return new Result(null, e);
                    }
                };
    }
}
