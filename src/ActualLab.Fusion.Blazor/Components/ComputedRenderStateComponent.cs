namespace ActualLab.Fusion.Blazor;

/// <summary>
/// Static configuration holder for <see cref="ComputedRenderStateComponent{TState}"/>.
/// </summary>
public static class ComputedRenderStateComponent
{
    public static ComputedStateComponentOptions DefaultOptions { get; set; }
        = ComputedStateComponentOptions.RecomputeStateOnParameterChange; // Doesn't need any standard render points
}

/// <summary>
/// A computed state component that tracks render state snapshots to avoid
/// redundant re-renders when the state has not changed.
/// </summary>
public abstract class ComputedRenderStateComponent<TState> : ComputedStateComponent<TState>
{
    private StateSnapshot? _renderState;

    protected StateSnapshot RenderState {
        get => _renderState ??= UntypedState.Snapshot;
        set => _renderState = value;
    }

    protected IEqualityComparer<TState>? StateEqualityComparer { get; set; }

    protected ComputedRenderStateComponent()
    {
        MustRenderAfterEvent = false; // See ShouldRender, it blocks renders unless State is changed
        Options = ComputedRenderStateComponent.DefaultOptions;
    }

    protected override bool ShouldRender()
    {
        var oldRenderState = _renderState;
        var newRenderState = UntypedState.Snapshot;
        if (!MustUpdateRenderState(oldRenderState, newRenderState))
            return false;

        // RenderState must track what is actually rendered, so it advances only when we render
        _renderState = newRenderState;
        return true;
    }

    protected bool MustUpdateRenderState(StateSnapshot? oldRenderState, StateSnapshot newRenderState)
    {
        if (oldRenderState is not null) {
            if (ReferenceEquals(oldRenderState, newRenderState))
                return false; // Same state
            if (StateEqualityComparer is { } stateEqualityComparer
                && oldRenderState.Computed is Computed<TState> oldComputed
                && newRenderState.Computed is Computed<TState> newComputed
                && !oldComputed.HasError && !newComputed.HasError
                && stateEqualityComparer.Equals(oldComputed.Value, newComputed.Value))
                return false; // Identical state
        }

        var computed = newRenderState.Computed;
        if (computed.IsConsistent() || computed.HasError)
            return true;

        // Inconsistent state is rare, so we make this check at last
        return Options.HasFlag(ComputedStateComponentOptions.RenderInconsistentState);
    }
}
