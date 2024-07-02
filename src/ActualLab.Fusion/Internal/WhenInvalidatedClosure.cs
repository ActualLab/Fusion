namespace ActualLab.Fusion.Internal;

internal sealed class WhenInvalidatedClosure
{
    private readonly Action<Computed> _onInvalidatedHandler;
    private readonly TaskCompletionSource<Unit> _taskSource;
    private readonly Computed _computed;
    private readonly CancellationTokenRegistration _cancellationTokenRegistration;

    public Task Task => _taskSource.Task;

    internal WhenInvalidatedClosure(TaskCompletionSource<Unit> taskSource, Computed computed, CancellationToken cancellationToken)
    {
        _taskSource = taskSource;
        _computed = computed;
        _onInvalidatedHandler = OnInvalidated;
        _computed.Invalidated += _onInvalidatedHandler;
        _cancellationTokenRegistration = cancellationToken.Register(OnUnregister);
    }

    private void OnInvalidated(Computed _)
    {
        _taskSource.TrySetResult(default);
        _cancellationTokenRegistration.Dispose();
    }

    private void OnUnregister()
    {
        _taskSource.TrySetCanceled();
        _computed.Invalidated -= _onInvalidatedHandler;
    }
}
