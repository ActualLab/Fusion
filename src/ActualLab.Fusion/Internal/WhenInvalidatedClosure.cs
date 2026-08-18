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
        // Unregister() rather than Dispose(): this can run from inside Computed's lock (see the
        // add accessor of its Invalidated event), and Dispose() would wait there for OnUnregister,
        // which needs that same lock to unsubscribe.
        _taskSource.TrySetResult();
        _cancellationTokenRegistration.Unregister();
    }

    private void OnUnregister()
    {
        _taskSource.TrySetException(new OperationCanceledException(_cancellationToken));
        _computed.Invalidated -= _onInvalidatedHandler;
    }
}
