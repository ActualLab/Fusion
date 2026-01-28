namespace ActualLab.Async;

public static partial class AsyncTaskMethodBuilderExt
{
#if USE_UNSAFE_ACCESSORS
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_task")]
    private static extern ref Task<T>? TaskGetter<T>(ref AsyncTaskMethodBuilder<T> builder);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncTaskMethodBuilder<T> FromTask<T>(Task<T> task)
    {
        AsyncTaskMethodBuilder<T> builder = default;
        TaskGetter(ref builder) = task;
        return builder;
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncTaskMethodBuilder<T> New<T>()
    {
        var builder = AsyncTaskMethodBuilder<T>.Create();
        var task = builder.Task; // This is super important, otherwise any of future calls will modify the local struct only
        TaskExt.SetRunContinuationsAsynchronouslyFlag(task);
        return builder;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncTaskMethodBuilder<T> New<T>(bool runContinuationsAsynchronously)
    {
        var builder = AsyncTaskMethodBuilder<T>.Create();
        var task = builder.Task; // This is super important, otherwise any of future calls will modify the local struct only
        if (runContinuationsAsynchronously)
            TaskExt.SetRunContinuationsAsynchronouslyFlag(task);
        return builder;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetCanceled<T>(this AsyncTaskMethodBuilder<T> target, CancellationToken cancellationToken)
        => target.SetException(new OperationCanceledException(cancellationToken));

    // TrySetXxx

    public static bool TrySetResult<T>(this AsyncTaskMethodBuilder<T> target, T result)
    {
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
        try {
            target.SetException(exception);
            return true;
        }
        catch (InvalidOperationException) {
            return false;
        }
    }

    public static bool TrySetCanceled<T>(this AsyncTaskMethodBuilder<T> target)
        => target.TrySetCanceled(CancellationTokenExt.Canceled);
    public static bool TrySetCanceled<T>(this AsyncTaskMethodBuilder<T> target, CancellationToken cancellationToken)
    {
        try {
            target.SetException(new OperationCanceledException(cancellationToken));
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncTaskMethodBuilder<T> WithCancellation<T>(this AsyncTaskMethodBuilder<T> target, CancellationToken cancellationToken)
    {
        target.SetException(new OperationCanceledException(cancellationToken));
        return target;
    }

    // (Try)SetFromTask

    public static void SetFromTask<T>(this AsyncTaskMethodBuilder<T> target, Task<T> task)
    {
        if (task.Exception is not null)
            target.SetException(task.Exception.GetBaseException());
        else
            target.SetResult(task.GetAwaiter().GetResult());
    }

    public static void TrySetFromTask<T>(this AsyncTaskMethodBuilder<T> target, Task<T> task)
    {
        if (task.Exception is not null)
            target.TrySetException(task.Exception.GetBaseException());
        else
            target.TrySetResult(task.GetAwaiter().GetResult());
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
        if (error is null)
            target.SetResult(value);
        else
            target.SetException(error);
    }

    public static bool TrySetFromResult<T>(this AsyncTaskMethodBuilder<T> target, Result<T> result)
    {
        var (value, error) = result;
        return error is null
            ? target.TrySetResult(value)
            : target.TrySetException(error);
    }
}
