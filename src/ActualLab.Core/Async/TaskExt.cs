using ActualLab.Internal;

namespace ActualLab.Async;

#pragma warning disable MA0004

public static partial class TaskExt
{
#if USE_UNSAFE_ACCESSORS
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_stateFlags")]
    private static extern ref int StateFlagsGetter(Task task);
#else
    private static readonly Action<Task, int> StateFlagsSetter;
#endif

    public static readonly Task<Unit> UnitTask;
    public static readonly Task<bool> TrueTask;
    public static readonly Task<bool> FalseTask;

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume Task class is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "We assume Task class is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume Task class is fully preserved")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(TaskExt))]
    static TaskExt()
    {
        UnitTask = Task.FromResult(Unit.Default);
        TrueTask = Task.FromResult(true);
        FalseTask = Task.FromResult(false);
#if !USE_UNSAFE_ACCESSORS
        StateFlagsSetter = typeof(Task)
            .GetField("m_stateFlags", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetSetter<Task, int>();
#endif
    }

    // NeverEnding - a shortcut for Task.Delay(Timeout.Infinite)

    public static Task NeverEnding(CancellationToken cancellationToken)
        => Task.Delay(Timeout.Infinite, cancellationToken);

    // NewNeverEndingUnreferenced

    // The tasks these methods return aren't referenced,
    // so unless whatever awaits them is referenced,
    // it may simply evaporate on the next GC cycle.
    //
    // Earlier such tasks were stored in a static var, which is actually wrong:
    // if one of them gets N dependencies, all of these N dependencies will stay
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
        return source.IsCompletedSuccessfully ? UnitTask : ConvertAsync(source);

        static async Task<Unit> ConvertAsync(Task source)
        {
            await source.ConfigureAwait(false);
            return default;
        }
    }

    // RequireResult

    public static void RequireResult(this Task task)
    {
        if (!task.IsCompleted)
            throw Errors.TaskIsNotCompleted();

        task.GetAwaiter().GetResult();
    }

    public static T RequireResult<T>(this Task<T> task)
    {
        if (!task.IsCompleted)
            throw Errors.TaskIsNotCompleted();

        return task.GetAwaiter().GetResult();
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
    {
        if (task.IsFaulted)
            return task.Exception!.GetBaseException();
        if (task.IsCanceled)
            return new TaskCanceledException(task);

        throw Errors.TaskIsNeitherFaultedNorCancelled();
    }

    // IsCanceledOrFaultedWithOce

    public static bool IsCanceledOrFaultedWithOce(this Task task)
        => task.IsCanceled || (task.IsFaulted && task.Exception?.GetBaseException() is OperationCanceledException);

    // AssertXxx

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task AssertCompleted(this Task task)
        => !task.IsCompleted ? throw Errors.TaskIsNotCompleted() : task;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T> AssertCompleted<T>(this Task<T> task)
        => !task.IsCompleted ? throw Errors.TaskIsNotCompleted() : task;

    // Internal methods

    internal static void SetRunContinuationsAsynchronouslyFlag(Task task)
    {
        // 0x2000400 = (int)TaskStateFlags.WaitingForActivation | (int)InternalTaskOptions.PromiseTask;
        const int stateFlags = 0x2000400 | (int)TaskContinuationOptions.RunContinuationsAsynchronously;
#if USE_UNSAFE_ACCESSORS
        StateFlagsGetter(task) = stateFlags;
#else
        StateFlagsSetter.Invoke(task, stateFlags);
#endif
    }
}
