namespace ActualLab.Async.Internal;

// Based on https://github.com/dotnet/runtime/issues/22144#issuecomment-1328319861

/// <summary>
/// An awaiter that silently ignores the result and errors of a <see cref="Task"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct SilentTaskAwaiter<TTask>(TTask task, bool captureContext = true)
    : ICriticalNotifyCompletion
    where TTask : Task
{
    public bool IsCompleted => task.IsCompleted;

    public SilentTaskAwaiter<TTask> GetAwaiter() => this;
    public void GetResult() { }

    public void OnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().OnCompleted(action);
    public void UnsafeOnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().UnsafeOnCompleted(action);
}
