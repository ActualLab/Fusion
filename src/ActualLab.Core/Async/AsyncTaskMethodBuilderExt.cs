namespace ActualLab.Async;

public static partial class AsyncTaskMethodBuilderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncTaskMethodBuilder<T> New<T>()
    {
        var result = AsyncTaskMethodBuilder<T>.Create();
        _ = result.Task;
        return result;
    }

    // TrySetResult / TrySetException

    public static bool TrySetResult<T>(this AsyncTaskMethodBuilder<T> target, T result)
    {
        var task = target.Task;
        if (task.IsCompleted)
            return false;

        try {
            target.SetResult(result);
            return true;
        }
        catch (InvalidOperationException) {
            return false;
        }
    }

    public static bool TrySetException<T>(this AsyncTaskMethodBuilder<T> target, Exception exception)
    {
        var task = target.Task;
        if (task.IsCompleted)
            return false;

        try {
            target.SetException(exception);
            return true;
        }
        catch (InvalidOperationException) {
            return false;
        }
    }

    // WithXxx

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncTaskMethodBuilder<T> WithResult<T>(this AsyncTaskMethodBuilder<T> target, T result)
    {
        target.SetResult(result);
        return target;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncTaskMethodBuilder<T> WithException<T>(this AsyncTaskMethodBuilder<T> target, Exception error)
    {
        target.SetException(error);
        return target;
    }

    // (Try)SetFromTask

    public static void SetFromTask<T>(this AsyncTaskMethodBuilder<T> target, Task<T> task)
    {
        if (task.Exception != null)
            target.SetException(task.Exception.GetBaseException());
        else
            target.SetResult(task.Result);
    }

    public static void TrySetFromTask<T>(this AsyncTaskMethodBuilder<T> target, Task<T> task)
    {
        if (task.Exception != null)
            target.TrySetException(task.Exception.GetBaseException());
        else
            target.TrySetResult(task.Result);
    }

    // (Try)SetFromTaskAsync

    public static Task SetFromTaskAsync<T>(this AsyncTaskMethodBuilder<T> target, Task<T> task)
    {
        _ = task.ContinueWith(
            t => target.SetFromTask(t),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return target.Task;
    }

    public static Task TrySetFromTaskAsync<T>(this AsyncTaskMethodBuilder<T> target, Task<T> task)
    {
        _ = task.ContinueWith(
            t => target.TrySetFromTask(t),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return target.Task;
    }

    // (Try)SetFromResult

    public static void SetFromResult<T>(this AsyncTaskMethodBuilder<T> target, Result<T> result)
    {
        var (value, error) = result;
        if (error == null)
            target.SetResult(value);
        else
            target.SetException(error);
    }

    public static void TrySetFromResult<T>(this AsyncTaskMethodBuilder<T> target, Result<T> result)
    {
        var (value, error) = result;
        if (error == null)
            target.TrySetResult(value);
        else
            target.TrySetException(error);
    }
}
