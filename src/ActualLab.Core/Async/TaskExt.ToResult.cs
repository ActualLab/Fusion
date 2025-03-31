namespace ActualLab.Async;

public static partial class TaskExt
{
    // ToResultSynchronously

    public static Result<Unit> ToResultSynchronously(this Task task)
        => task.AssertCompleted().IsCompletedSuccessfully()
            ? default
            : new Result<Unit>(default, task.GetBaseException());

    public static Result<T> ToResultSynchronously<T>(this Task<T> task)
        => task.AssertCompleted().IsCompletedSuccessfully()
            ? task.GetAwaiter().GetResult()
            : new Result<T>(default!, task.GetBaseException());

    // ToResultAsync

    public static async Task<Result<Unit>> ToResultAsync(this Task task)
    {
        try {
            await task.ConfigureAwait(false);
            return default;
        }
        catch (Exception e) {
            return new Result<Unit>(default, e);
        }
    }

    public static async Task<Result<T>> ToResultAsync<T>(this Task<T> task)
    {
        try {
            return await task.ConfigureAwait(false);
        }
        catch (Exception e) {
            return new Result<T>(default!, e);
        }
    }
}
