namespace ActualLab.Async.Internal;

// Based on https://github.com/dotnet/runtime/issues/22144#issuecomment-1328319861

[StructLayout(LayoutKind.Auto)]
public readonly struct SuppressCancellationValueTaskAwaiter(ValueTask task, bool captureContext = true)
    : ICriticalNotifyCompletion
{
    public bool IsCompleted => task.IsCompleted;

    public SuppressCancellationValueTaskAwaiter GetAwaiter() => this;

    public void GetResult()
    {
        if (task.IsCanceled)
            return;
        task.GetAwaiter().GetResult();
    }

    public void OnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().OnCompleted(action);
    public void UnsafeOnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().UnsafeOnCompleted(action);
}

[StructLayout(LayoutKind.Auto)]
public readonly struct SuppressCancellationValueTaskAwaiter<T>(ValueTask<T> task, bool captureContext = true)
    : ICriticalNotifyCompletion
{
    public bool IsCompleted => task.IsCompleted;

    public SuppressCancellationValueTaskAwaiter<T> GetAwaiter() => this;
    public T GetResult() => task.IsCanceled ? default! : task.GetAwaiter().GetResult();

    public void OnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().OnCompleted(action);
    public void UnsafeOnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().UnsafeOnCompleted(action);
}
