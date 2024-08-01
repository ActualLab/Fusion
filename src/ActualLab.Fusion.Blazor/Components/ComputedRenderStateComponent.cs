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

        if ((Options & ComputedStateComponentOptions.ShouldRenderInconsistentState) != 0)
            return true;

        return State.Computed.IsConsistent();
    }

    protected bool IsRenderStateChanged()
        => IsRenderStateChanged(State.Snapshot);

    protected bool IsRenderStateChanged(StateSnapshot<TState> renderState)
    {
        if (_renderState == renderState)
            return false;

        _renderState = renderState;
        return true;
    }
}
