namespace ActualLab.Async;

public static partial class AsyncTaskMethodBuilderExt
{
#if USE_UNSAFE_ACCESSORS
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_task")]
    private static extern ref Task? TaskGetter(ref AsyncTaskMethodBuilder builder);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncTaskMethodBuilder FromTask(Task task)
    {
        AsyncTaskMethodBuilder builder = default;
        TaskGetter(ref builder) = task;
        return builder;
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncTaskMethodBuilder New()
    {
        var builder = AsyncTaskMethodBuilder.Create();
        var task = builder.Task; // This is super important, otherwise any of future calls will modify the local struct only
        TaskExt.SetRunContinuationsAsynchronouslyFlag(task);
        return builder;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncTaskMethodBuilder New(bool runContinuationsAsynchronously)
    {
        var builder = AsyncTaskMethodBuilder.Create();
        var task = builder.Task; // This is super important, otherwise any of future calls will modify the local struct only
        if (runContinuationsAsynchronously)
            TaskExt.SetRunContinuationsAsynchronouslyFlag(task);
        return builder;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetCanceled(this AsyncTaskMethodBuilder target, CancellationToken cancellationToken)
        => target.SetException(new OperationCanceledException(cancellationToken));

    // TrySetXxx

    public static bool TrySetResult(this AsyncTaskMethodBuilder target)
    {
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
        try {
            target.SetException(exception);
            return true;
        }
        catch (InvalidOperationException) {
            return false;
        }
    }

    public static bool TrySetCanceled(this AsyncTaskMethodBuilder target)
        => target.TrySetCanceled(CancellationTokenExt.Canceled);
    public static bool TrySetCanceled(this AsyncTaskMethodBuilder target, CancellationToken cancellationToken)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncTaskMethodBuilder WithCancellation(this AsyncTaskMethodBuilder target, CancellationToken cancellationToken)
    {
        target.SetException(new OperationCanceledException(cancellationToken));
        return target;
    }

    // (Try)SetFromTask

    public static void SetFromTask(this AsyncTaskMethodBuilder target, Task task)
    {
        if (task.Exception is not null)
            target.SetException(task.Exception.GetBaseException());
        else
            target.SetResult();
    }

    public static void TrySetFromTask(this AsyncTaskMethodBuilder target, Task task)
    {
        if (task.Exception is not null)
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
        if (error is null)
            target.SetResult();
        else
            target.SetException(error);
    }

    public static bool TrySetFromResult(this AsyncTaskMethodBuilder target, Result<Unit> result)
    {
        var error = result.Error;
        return error is null
            ? target.TrySetResult()
            : target.TrySetException(error);
    }
}
