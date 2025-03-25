using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;
using ActualLab.OS;

namespace ActualLab.Async;

#pragma warning disable MA0004

public static partial class TaskExt
{
    private static readonly MethodInfo FromTypedTaskInternalMethod
        = typeof(TaskExt).GetMethod(nameof(FromTypedTaskInternal), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly ConcurrentDictionary<Type, Func<Task, IResult>> ToTypedResultCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static readonly Task<Unit> UnitTask = Task.FromResult(Unit.Default);
    public static readonly Task<bool> TrueTask = Task.FromResult(true);
    public static readonly Task<bool> FalseTask = Task.FromResult(false);

    // NewNeverEndingUnreferenced

    // The tasks these methods return aren't referenced,
    // so unless whatever awaits them is referenced,
    // it may simply evaporate on the next GC cycle.
    //
    // Earlier such tasks were stored in a static var, which is actually wrong:
    // if one of them get N dependencies, all of these N dependencies will stay
    // in RAM forever, since there is no way to "unsubscribe" an awaiter.
    //
    // So the best option here is to return a task that won't prevent
    // GC from collecting the awaiter in case nothing else "holds" it -
    // and assuming the task is really never ending, this is the right thing to do.
    public static Task NewNeverEndingUnreferenced()
        => AsyncTaskMethodBuilderExt.New<Unit>().Task;
    public static Task<T> NewNeverEndingUnreferenced<T>()
        => AsyncTaskMethodBuilderExt.New<T>().Task;

    // ToValueTask

    public static ValueTask<T> ToValueTask<T>(this Task<T> source) => new(source);
    public static ValueTask ToValueTask(this Task source) => new(source);

    // ToUnitTask

    public static Task<Unit> ToUnitTask(this Task source)
    {
        return source.IsCompletedSuccessfully() ? UnitTask : ConvertAsync(source);

        static async Task<Unit> ConvertAsync(Task source)
        {
            await source.ConfigureAwait(false);
            return default;
        }
    }

    // GetResultKind

    public static TaskResultKind GetResultKind(this Task task)
    {
        if (!task.IsCompleted)
            return TaskResultKind.Incomplete;
        if (task.IsCanceled)
            return TaskResultKind.Cancellation;
        return task.IsFaulted ? TaskResultKind.Error : TaskResultKind.Success;
    }

    // GetBaseException

    public static Exception GetBaseException(this Task task)
        => task.AssertCompleted().Exception?.GetBaseException()
            ?? (task.IsCanceled
                ? new TaskCanceledException(task)
                : throw Errors.TaskIsFaultedButNoExceptionAvailable());

    // AssertXxx

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task AssertCompleted(this Task task)
        => !task.IsCompleted ? throw Errors.TaskIsNotCompleted() : task;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T> AssertCompleted<T>(this Task<T> task)
        => !task.IsCompleted ? throw Errors.TaskIsNotCompleted() : task;

    // Private methods

    private static IResult FromTypedTaskInternal<T>(Task task)
        // ReSharper disable once HeapView.BoxingAllocation
        => task.IsCompletedSuccessfully()
            ? new Result<T>(((Task<T>)task).Result)
            : new Result<T>(default!, task.GetBaseException());
}
