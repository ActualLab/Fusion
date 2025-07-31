namespace ActualLab.Fusion;

public sealed class StateSnapshot
{
    private AsyncTaskMethodBuilder _whenUpdatingSource = AsyncTaskMethodBuilderExt.New();
    private AsyncTaskMethodBuilder _whenUpdatedSource = AsyncTaskMethodBuilderExt.New();

    public readonly State State;
    public readonly Computed Computed;
    public readonly Computed LastNonErrorComputed;
    public int UpdateCount { get; }
    public int ErrorCount { get; }
    public int RetryCount { get; }
    public bool IsInitial => UpdateCount == 0;

    public StateSnapshot(State state, StateSnapshot? prevSnapshot, Computed computed)
    {
        State = state;
        Computed = computed;
        LastNonErrorComputed = computed;
        if (prevSnapshot is null) {
            UpdateCount = 0;
            ErrorCount = 0;
            RetryCount = 0;
            return;
        }

        var error = computed.Error;
        if (error is null) {
            LastNonErrorComputed = computed;
            UpdateCount = 1 + prevSnapshot.UpdateCount;
            ErrorCount = prevSnapshot.ErrorCount;
            RetryCount = 0;
        }
        else if (!computed.IsTransientError(error)) {
            // Non-transient error
            LastNonErrorComputed = prevSnapshot.LastNonErrorComputed;
            UpdateCount = 1 + prevSnapshot.UpdateCount;
            ErrorCount = 1 + prevSnapshot.ErrorCount;
            RetryCount = 0;
        }
        else {
            // Transient error
            LastNonErrorComputed = prevSnapshot.LastNonErrorComputed;
            UpdateCount = 1 + prevSnapshot.UpdateCount;
            ErrorCount = 1 + prevSnapshot.ErrorCount;
            RetryCount = 1 + prevSnapshot.RetryCount;
        }
    }

    public override string ToString()
        => $"{GetType().GetName()}({Computed}, [{UpdateCount} update(s) / {ErrorCount} failure(s)])";

    public Task WhenInvalidated(CancellationToken cancellationToken = default)
        => Computed.WhenInvalidated(cancellationToken);
    public Task WhenUpdating() => _whenUpdatingSource.Task;
    public Task WhenUpdated() => _whenUpdatedSource.Task;

    internal void OnUpdating()
        => _whenUpdatingSource.TrySetResult();

    internal void OnUpdated()
    {
        _whenUpdatingSource.TrySetResult();
        _whenUpdatedSource.TrySetResult();
    }
}
