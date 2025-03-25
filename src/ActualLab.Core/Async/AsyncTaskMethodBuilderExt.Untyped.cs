namespace ActualLab.Async;

public static partial class AsyncTaskMethodBuilderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncTaskMethodBuilder New()
    {
        var result = AsyncTaskMethodBuilder.Create();
        _ = result.Task;
        return result;
    }

    // TrySetResult / TrySetException

    public static bool TrySetResult(this AsyncTaskMethodBuilder target)
    {
        var task = target.Task;
        if (task.IsCompleted)
            return false;

        try {
            target.SetResult();
            return true;
        }
        catch (InvalidOperationException) {
            return false;
        }
    }

    public static bool TrySetException(this AsyncTaskMethodBuilder target, Exception exception)
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
    public static AsyncTaskMethodBuilder WithResult(this AsyncTaskMethodBuilder target)
    {
        target.SetResult();
        return target;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncTaskMethodBuilder WithException(this AsyncTaskMethodBuilder target, Exception error)
    {
        target.SetException(error);
        return target;
    }

    // (Try)SetFromTask

    public static void SetFromTask(this AsyncTaskMethodBuilder target, Task task)
    {
        if (task.Exception != null)
            target.SetException(task.Exception.GetBaseException());
        else
            target.SetResult();
    }

    public static void TrySetFromTask(this AsyncTaskMethodBuilder target, Task task)
    {
        if (task.Exception != null)
            target.TrySetException(task.Exception.GetBaseException());
        else
            target.TrySetResult();
    }

    // (Try)SetFromTaskAsync

    public static Task SetFromTaskAsync(this AsyncTaskMethodBuilder target, Task task)
    {
        _ = task.ContinueWith(
            t => target.SetFromTask(t),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return target.Task;
    }

    public static Task TrySetFromTaskAsync(this AsyncTaskMethodBuilder target, Task task)
    {
        _ = task.ContinueWith(
            t => target.TrySetFromTask(t),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return target.Task;
    }

    // (Try)SetFromResult

    public static void SetFromResult(this AsyncTaskMethodBuilder target, Result<Unit> result)
    {
        var error = result.Error;
        if (error == null)
            target.SetResult();
        else
            target.SetException(error);
    }

    public static void TrySetFromResult(this AsyncTaskMethodBuilder target, Result<Unit> result)
    {
        var error = result.Error;
        if (error == null)
            target.TrySetResult();
        else
            target.TrySetException(error);
    }
}
