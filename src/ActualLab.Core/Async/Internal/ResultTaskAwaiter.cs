namespace ActualLab.Async.Internal;

// Based on https://github.com/dotnet/runtime/issues/22144#issuecomment-1328319861

/// <summary>
/// An awaiter that returns a <see cref="Result{T}"/> of <see cref="Unit"/> from a <see cref="Task"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct ResultTaskAwaiter(Task task, bool captureContext = true)
    : ICriticalNotifyCompletion
{
    public bool IsCompleted => task.IsCompleted;

    public ResultTaskAwaiter GetAwaiter() => this;
    public Result<Unit> GetResult() => task.ToResultSynchronously();

    public void OnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().OnCompleted(action);
    public void UnsafeOnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().UnsafeOnCompleted(action);
}

/// <summary>
/// An awaiter that returns a <see cref="Result{T}"/> from a <see cref="Task{TResult}"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct ResultTaskAwaiter<T>(Task<T> task, bool captureContext = true)
    : ICriticalNotifyCompletion
{
    public bool IsCompleted => task.IsCompleted;

    public ResultTaskAwaiter<T> GetAwaiter() => this;
    public Result<T> GetResult() => task.ToResultSynchronously();

    public void OnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().OnCompleted(action);
    public void UnsafeOnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().UnsafeOnCompleted(action);
}
