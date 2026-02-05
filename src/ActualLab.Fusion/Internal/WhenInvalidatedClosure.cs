namespace ActualLab.Fusion.Internal;

/// <summary>
/// A closure that completes a <see cref="Task"/> when a <see cref="Computed"/> is invalidated
/// or a <see cref="CancellationToken"/> is triggered.
/// </summary>
internal sealed class WhenInvalidatedClosure
{
    private readonly Action<Computed> _onInvalidatedHandler;
    private readonly AsyncTaskMethodBuilder _taskSource;
    private readonly Computed _computed;
    private readonly CancellationToken _cancellationToken;
    private readonly CancellationTokenRegistration _cancellationTokenRegistration;

    public Task Task => _taskSource.Task;

    internal WhenInvalidatedClosure(AsyncTaskMethodBuilder taskSource, Computed computed, CancellationToken cancellationToken)
    {
        _taskSource = taskSource;
        _computed = computed;
        _onInvalidatedHandler = OnInvalidated;
        _computed.Invalidated += _onInvalidatedHandler;
        _cancellationToken = cancellationToken;
        _cancellationTokenRegistration = cancellationToken.Register(OnUnregister);
    }

    private void OnInvalidated(Computed _)
    {
        _taskSource.TrySetResult();
        _cancellationTokenRegistration.Dispose();
    }

    private void OnUnregister()
    {
        _taskSource.TrySetException(new OperationCanceledException(_cancellationToken));
        _computed.Invalidated -= _onInvalidatedHandler;
    }
}
