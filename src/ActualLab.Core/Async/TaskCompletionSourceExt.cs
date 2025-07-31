namespace ActualLab.Async;

#pragma warning disable CA2016

public static partial class TaskCompletionSourceExt
{
    // NewXxx

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaskCompletionSource<T> New<T>()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaskCompletionSource<T> New<T>(bool runContinuationsAsynchronously)
        => runContinuationsAsynchronously
            ? new(TaskCreationOptions.RunContinuationsAsynchronously)
            : new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaskCompletionSource<T> New<T>(TaskCreationOptions taskCreationOptions)
        => new(taskCreationOptions);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaskCompletionSource<T> New<T>(object? state, TaskCreationOptions taskCreationOptions)
        => new(state, taskCreationOptions);

#if !NET5_0_OR_GREATER
    // TrySetCanceled overload for pre-NET5

    public static bool TrySetCanceled<T>(this TaskCompletionSource<T> target, CancellationToken cancellationToken)
        => target.TrySetCanceled();
#endif

    // WithXxx

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaskCompletionSource<T> WithResult<T>(this TaskCompletionSource<T> target, T result)
    {
        target.TrySetResult(result);
        return target;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaskCompletionSource<T> WithException<T>(this TaskCompletionSource<T> target, Exception error)
    {
        target.TrySetException(error);
        return target;
    }

    public static TaskCompletionSource<T> WithCancellation<T>(this TaskCompletionSource<T> target, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            target.TrySetCanceled(cancellationToken);
        else
            target.TrySetCanceled();
        return target;
    }

    // (Try)SetFromTask

    public static void SetFromTask<T>(this TaskCompletionSource<T> target, Task<T> task, CancellationToken cancellationToken = default)
    {
        if (task.IsCanceled) {
#if NET5_0_OR_GREATER
            if (cancellationToken.IsCancellationRequested)
                target.SetCanceled(cancellationToken);
            else
                // ReSharper disable once MethodSupportsCancellation
                target.SetCanceled();
#else
            target.SetCanceled();
#endif
        }
        else if (task.Exception is not null)
            target.SetException(task.Exception.GetBaseException());
        else
            target.SetResult(task.Result);
    }

    public static bool TrySetFromTask<T>(this TaskCompletionSource<T> target, Task<T> task, CancellationToken cancellationToken = default)
        => task.IsCanceled
            ? cancellationToken.IsCancellationRequested
                ? target.TrySetCanceled(cancellationToken)
                : target.TrySetCanceled()
            : task.Exception is not null
                ? target.TrySetException(task.Exception.GetBaseException())
                : target.TrySetResult(task.Result);

    // (Try)SetFromTaskAsync

    public static Task SetFromTaskAsync<T>(this TaskCompletionSource<T> target, Task<T> task, CancellationToken cancellationToken = default)
    {
        _ = task.ContinueWith(
            t => target.SetFromTask(t, cancellationToken),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return target.Task;
    }

    public static Task TrySetFromTaskAsync<T>(this TaskCompletionSource<T> target, Task<T> task, CancellationToken cancellationToken = default)
    {
        _ = task.ContinueWith(
            t => target.TrySetFromTask(t, cancellationToken),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return target.Task;
    }

    // (Try)SetFromResult

    public static void SetFromResult<T>(this TaskCompletionSource<T> target, Result<T> result)
    {
        var (value, error) = result;
        if (error is null)
            target.SetResult(value);
        else if (error is OperationCanceledException)
            target.SetCanceled();
        else
            target.SetException(error);
    }

    public static void SetFromResult<T>(this TaskCompletionSource<T> target, Result<T> result, CancellationToken cancellationToken)
    {
        var (value, error) = result;
        if (error is null)
            target.SetResult(value);
        else if (error is OperationCanceledException) {
#if NET5_0_OR_GREATER
            if (cancellationToken.IsCancellationRequested)
                target.SetCanceled(cancellationToken);
            else
                // ReSharper disable once MethodSupportsCancellation
                target.SetCanceled();
#else
            target.SetCanceled();
#endif
        }
        else
            target.SetException(error);
    }

    public static bool TrySetFromResult<T>(this TaskCompletionSource<T> target, Result<T> result)
    {
        var (value, error) = result;
        return error is null
            ? target.TrySetResult(value)
            : error is OperationCanceledException
                ? target.TrySetCanceled()
                : target.TrySetException(error);
    }

    public static bool TrySetFromResult<T>(this TaskCompletionSource<T> target, Result<T> result, CancellationToken cancellationToken)
    {
        var (value, error) = result;
        return error is null
            ? target.TrySetResult(value)
            : error is OperationCanceledException
                ? cancellationToken.IsCancellationRequested
                    ? target.TrySetCanceled(cancellationToken)
                    : target.TrySetCanceled()
                : target.TrySetException(error);
    }
}
