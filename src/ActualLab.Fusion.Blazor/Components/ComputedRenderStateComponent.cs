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

    protected ComputedRenderStateComponent()
        => Options = ComputedRenderStateComponent.DefaultOptions;

    protected override bool ShouldRender()
    {
        if (!IsRenderStateChanged())
            return false;

        var computed = RenderState.Computed;
        if (computed.IsConsistent() || computed.HasError)
            return true;

        // Inconsistent state is rare, so we make this check at last
        return (Options & ComputedStateComponentOptions.RenderInconsistentState) != 0;
    }

    protected bool IsRenderStateChanged()
        => IsRenderStateChanged(UntypedState.Snapshot);

    protected bool IsRenderStateChanged(StateSnapshot renderState)
    {
        if (ReferenceEquals(_renderState, renderState))
            return false;

        _renderState = renderState;
        return true;
    }
}
