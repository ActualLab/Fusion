using ActualLab.Internal;

namespace ActualLab.Fusion.Blazor;

public abstract class MixedStateComponent<TState, TMutableState> : ComputedStateComponent<TState>
{
    protected MutableState<TMutableState> MutableState { get; private set; } = null!;

    protected override void OnInitialized()
    {
        if (ReferenceEquals(MutableState, null))
            SetMutableState(CreateMutableState());
        base.OnInitialized();
    }

    protected void SetMutableState(MutableState<TMutableState> mutableState)
    {
        if (MutableState != null)
            throw Errors.AlreadyInitialized(nameof(MutableState));

        MutableState = mutableState ?? throw new ArgumentNullException(nameof(mutableState));
        mutableState.Updated += (_, _) => _ = State.Recompute();
    }

    protected virtual string GetMutableStateCategory()
        => ComputedStateComponent.GetMutableStateCategory(GetType());

    protected virtual MutableState<TMutableState>.Options GetMutableStateOptions()
        => new() { Category = GetMutableStateCategory() };

    protected virtual MutableState<TMutableState> CreateMutableState()
        => StateFactory.NewMutable(GetMutableStateOptions());
}
