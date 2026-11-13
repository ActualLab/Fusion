namespace ActualLab.Async.Internal;

// Based on https://github.com/dotnet/runtime/issues/22144#issuecomment-1328319861

/// <summary>
/// An awaiter that silently ignores the result and errors of a <see cref="ValueTask"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct SilentValueTaskAwaiter(ValueTask task, bool captureContext = true)
    : ICriticalNotifyCompletion
{
    public bool IsCompleted => task.IsCompleted;

    public SilentValueTaskAwaiter GetAwaiter() => this;

    public void GetResult()
    {
        // A ValueTask must be consumed: that's what releases its IValueTaskSource
        // and marks the error of the Task it may wrap as observed
        if (task.IsCompletedSuccessfully)
            task.GetAwaiter().GetResult();
        else
            _ = task.AsTask().Exception;
    }

    public void OnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().OnCompleted(action);
    public void UnsafeOnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().UnsafeOnCompleted(action);
}

/// <summary>
/// An awaiter that silently ignores the result and errors of a <see cref="ValueTask{TResult}"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct VoidValueTaskAwaiter<T>(ValueTask<T> task, bool captureContext = true)
    : ICriticalNotifyCompletion
{
    public bool IsCompleted => task.IsCompleted;

    public VoidValueTaskAwaiter<T> GetAwaiter() => this;

    public void GetResult()
    {
        // See SilentValueTaskAwaiter.GetResult
        if (task.IsCompletedSuccessfully)
            _ = task.GetAwaiter().GetResult();
        else
            _ = task.AsTask().Exception;
    }

    public void OnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().OnCompleted(action);
    public void UnsafeOnCompleted(Action action)
        => task.ConfigureAwait(captureContext).GetAwaiter().UnsafeOnCompleted(action);
}
