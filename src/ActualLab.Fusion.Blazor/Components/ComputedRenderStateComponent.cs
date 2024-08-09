namespace ActualLab.Fusion.Blazor;

public abstract class ComputedRenderStateComponent<TState> : ComputedStateComponent<TState>
{
    private StateSnapshot<TState>? _renderState;

    protected StateSnapshot<TState> RenderState {
        get => _renderState ??= State.Snapshot;
        set => _renderState = value;
    }

    protected override bool ShouldRender()
    {
        if (!IsRenderStateChanged())
            return false;

        var computed = RenderState.Computed;
        if (computed.IsConsistent() || computed.HasError)
            return true;

        // Inconsistent state is rare, so we make this check at last
        return (Options & ComputedStateComponentOptions.ShouldRenderInconsistentState) != 0;
    }

    protected bool IsRenderStateChanged()
        => IsRenderStateChanged(State.Snapshot);

    protected bool IsRenderStateChanged(StateSnapshot<TState> renderState)
    {
        if (ReferenceEquals(_renderState, renderState))
            return false;

        _renderState = renderState;
        return true;
    }
}
