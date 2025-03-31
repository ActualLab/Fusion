namespace ActualLab.Fusion;

public abstract class StateSnapshot
{
    protected AsyncTaskMethodBuilder WhenUpdatingSource = AsyncTaskMethodBuilderExt.New();
    protected AsyncTaskMethodBuilder WhenUpdatedSource = AsyncTaskMethodBuilderExt.New();

    public abstract IState UntypedState { get; }
    public abstract Computed UntypedComputed { get; }
    public abstract Computed UntypedLastNonErrorComputed { get; }
    public int UpdateCount { get; protected init; }
    public int ErrorCount { get; protected init; }
    public int RetryCount { get; protected init; }
    public bool IsInitial => UpdateCount == 0;

    public override string ToString()
        => $"{GetType().GetName()}({UntypedComputed}, [{UpdateCount} update(s) / {ErrorCount} failure(s)])";

    public Task WhenInvalidated(CancellationToken cancellationToken = default)
        => UntypedComputed.WhenInvalidated(cancellationToken);
    public Task WhenUpdating() => WhenUpdatingSource.Task;
    public Task WhenUpdated() => WhenUpdatedSource.Task;

    protected internal void OnUpdating()
        => WhenUpdatingSource.TrySetResult();

    protected internal void OnUpdated()
    {
        WhenUpdatingSource.TrySetResult();
        WhenUpdatedSource.TrySetResult();
    }
}

public class StateSnapshot<T> : StateSnapshot
{
    public readonly IState<T> State;
    public readonly Computed<T> Computed;
    public readonly Computed<T> LastNonErrorComputed;

    public override IState UntypedState => State;
    public override Computed UntypedComputed => Computed;
    public override Computed UntypedLastNonErrorComputed => LastNonErrorComputed;

    public StateSnapshot(IState<T> state, Computed<T> computed)
    {
        State = state;
        Computed = computed;
        LastNonErrorComputed = computed;
        UpdateCount = 0;
        ErrorCount = 0;
        RetryCount = 0;
    }

    public StateSnapshot(StateSnapshot<T> prevSnapshot, Computed<T> computed)
    {
        State = prevSnapshot.State;
        Computed = computed;
        var error = computed.Error;
        if (error == null) {
            LastNonErrorComputed = computed;
            UpdateCount = 1 + prevSnapshot.UpdateCount;
            ErrorCount = prevSnapshot.ErrorCount;
            RetryCount = 0;
        }
        else {
            if (!computed.IsTransientError(error)) {
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
    }
}
