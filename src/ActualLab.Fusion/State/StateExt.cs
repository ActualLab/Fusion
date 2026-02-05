namespace ActualLab.Fusion;

/// <summary>
/// Extension methods for <see cref="IState"/>.
/// </summary>
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
        => (Task<T>)state.Computed.UseUntyped(allowInconsistent: false, cancellationToken);

    public static Task<T> Use<T>(
        this IState<T> state, bool allowInconsistent, CancellationToken cancellationToken = default)
        => (Task<T>)state.Computed.UseUntyped(allowInconsistent, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Invalidate(this IState state, bool immediately = false,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0)
        => state.Computed.Invalidate(immediately, new InvalidationSource(file, member, line));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Invalidate(this IState state, bool immediately, InvalidationSource source)
        => state.Computed.Invalidate(immediately, source);

    public static ValueTask Recompute(this IState state, CancellationToken cancellationToken = default)
    {
        state.Computed.Invalidate(immediately: true, InvalidationSource.StateExtRecompute);
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
