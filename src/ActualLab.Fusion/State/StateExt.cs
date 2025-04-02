namespace ActualLab.Fusion;

public static class StateExt
{
    // Computed-like methods

    public static ValueTask Update(this IState state, CancellationToken cancellationToken = default)
    {
        var valueTask = state.Computed.UpdateUntyped(cancellationToken);
        return valueTask.IsCompletedSuccessfully
            ? default
            : new ValueTask(valueTask.AsTask());
    }

    public static Task<T> Use<T>(
        this IState<T> state, CancellationToken cancellationToken = default)
        => (Task<T>)state.Computed.UseUntyped(cancellationToken);

    public static void Invalidate(this IState state, bool immediately = false)
        => state.Computed.Invalidate(immediately);

    public static ValueTask Recompute(this IState state, CancellationToken cancellationToken = default)
    {
        state.Computed.Invalidate(true);
        return state.Update(cancellationToken);
    }

    // WhenNonInitial

    public static Task WhenNonInitial(this IState state)
    {
        if (state is IMutableState)
            return Task.CompletedTask;

        var snapshot = state.Snapshot;
        return snapshot.IsInitial
            ? snapshot.WhenUpdated()
            : Task.CompletedTask;
    }

    // Add/RemoveEventHandler

    public static void AddEventHandler(this IState state,
        StateEventKind eventFilter, Action<State, StateEventKind> handler)
    {
        if ((eventFilter & StateEventKind.Invalidated) != 0)
            state.Invalidated += handler;
        if ((eventFilter & StateEventKind.Updating) != 0)
            state.Updating += handler;
        if ((eventFilter & StateEventKind.Updated) != 0)
            state.Updated += handler;
    }

    public static void RemoveEventHandler(this IState state,
        StateEventKind eventFilter, Action<State, StateEventKind> handler)
    {
        if ((eventFilter & StateEventKind.Invalidated) != 0)
            state.Invalidated -= handler;
        if ((eventFilter & StateEventKind.Updating) != 0)
            state.Updating -= handler;
        if ((eventFilter & StateEventKind.Updated) != 0)
            state.Updated -= handler;
    }
}
