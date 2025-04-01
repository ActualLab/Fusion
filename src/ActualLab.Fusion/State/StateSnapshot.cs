namespace ActualLab.Fusion;

public class StateSnapshot<T>(State<T> state, StateSnapshot<T>? prevSnapshot, Computed<T> computed)
    : StateSnapshot(state, prevSnapshot, computed)
{
    public State<T> State => (State<T>)UntypedState;
    public Computed<T> Computed => (Computed<T>)UntypedComputed;
    public Computed<T> LastNonErrorComputed => (Computed<T>)UntypedLastNonErrorComputed;
}

public abstract class StateSnapshot
{
    protected AsyncTaskMethodBuilder WhenUpdatingSource = AsyncTaskMethodBuilderExt.New();
    protected AsyncTaskMethodBuilder WhenUpdatedSource = AsyncTaskMethodBuilderExt.New();

    public readonly State UntypedState;
    public readonly Computed UntypedComputed;
    public readonly Computed UntypedLastNonErrorComputed;
    public int UpdateCount { get; protected init; }
    public int ErrorCount { get; protected init; }
    public int RetryCount { get; protected init; }
    public bool IsInitial => UpdateCount == 0;

    protected StateSnapshot(State state, StateSnapshot? prevSnapshot, Computed computed)
    {
        UntypedState = state;
        UntypedComputed = computed;
        UntypedLastNonErrorComputed = computed;
        if (prevSnapshot == null) {
            UpdateCount = 0;
            ErrorCount = 0;
            RetryCount = 0;
            return;
        }

        var error = computed.Error;
        if (error == null) {
            UntypedLastNonErrorComputed = computed;
            UpdateCount = 1 + prevSnapshot.UpdateCount;
            ErrorCount = prevSnapshot.ErrorCount;
            RetryCount = 0;
        }
        else if (!computed.IsTransientError(error)) {
            // Non-transient error
            UntypedLastNonErrorComputed = prevSnapshot.UntypedLastNonErrorComputed;
            UpdateCount = 1 + prevSnapshot.UpdateCount;
            ErrorCount = 1 + prevSnapshot.ErrorCount;
            RetryCount = 0;
        }
        else {
            // Transient error
            UntypedLastNonErrorComputed = prevSnapshot.UntypedLastNonErrorComputed;
            UpdateCount = 1 + prevSnapshot.UpdateCount;
            ErrorCount = 1 + prevSnapshot.ErrorCount;
            RetryCount = 1 + prevSnapshot.RetryCount;
        }
    }

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
