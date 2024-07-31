using ActualLab.Internal;

namespace ActualLab.Async;

#pragma warning disable CA2012

public static partial class ValueTaskExt
{
    public static readonly ValueTask CompletedTask = Task.CompletedTask.ToValueTask();
    public static readonly ValueTask<bool> TrueTask = FromResult(true);
    public static readonly ValueTask<bool> FalseTask = FromResult(false);

    public static ValueTask<T> FromResult<T>(T value) => new(value);
    public static ValueTask<T> FromException<T>(Exception error) => new(Task.FromException<T>(error));

    public static TaskResultKind GetResultKind(this ValueTask task)
    {
        if (!task.IsCompleted)
            return TaskResultKind.Incomplete;
        if (task.IsCanceled)
            return TaskResultKind.Cancellation;
        return task.IsFaulted ? TaskResultKind.Error : TaskResultKind.Success;
    }

    public static TaskResultKind GetResultKind<T>(this ValueTask<T> task)
    {
        if (!task.IsCompleted)
            return TaskResultKind.Incomplete;
        if (task.IsCanceled)
            return TaskResultKind.Cancellation;
        return task.IsFaulted ? TaskResultKind.Error : TaskResultKind.Success;
    }


    // ToResultSynchronously

    public static Result<Unit> ToResultSynchronously(this ValueTask task)
        => task.AssertCompleted().IsCompletedSuccessfully
            ? default
            : new Result<Unit>(default, task.AsTask().GetBaseException());

    public static Result<T> ToResultSynchronously<T>(this ValueTask<T> task)
        => task.AssertCompleted().IsCompletedSuccessfully
            ? task.Result
            : new Result<T>(default!, task.AsTask().GetBaseException());

    // ToResultAsync

    public static async ValueTask<Result<Unit>> ToResultAsync(this ValueTask task)
    {
        try {
            await task.ConfigureAwait(false);
            return default;
        }
        catch (Exception e) {
            return new Result<Unit>(default, e);
        }
    }

    public static async ValueTask<Result<T>> ToResultAsync<T>(this ValueTask<T> task)
    {
        try {
            return await task.ConfigureAwait(false);
        }
        catch (Exception e) {
            return new Result<T>(default!, e);
        }
    }

    // ToUnitTask

    public static Task<Unit> ToUnitTask(this ValueTask source)
    {
        return source.IsCompletedSuccessfully ? TaskExt.UnitTask : ConvertAsync(source);

        static async Task<Unit> ConvertAsync(ValueTask source)
        {
            await source.ConfigureAwait(false);
            return default;
        }
    }

    // AssertXxx

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask AssertCompleted(this ValueTask task)
        => !task.IsCompleted ? throw Errors.TaskIsNotCompleted() : task;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> AssertCompleted<T>(this ValueTask<T> task)
        => !task.IsCompleted ? throw Errors.TaskIsNotCompleted() : task;
}
