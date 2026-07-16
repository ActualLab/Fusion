using ActualLab.Internal;
using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

/// <summary>
/// A computed state component that also manages a <see cref="MutableState{T}"/>,
/// automatically recomputing when the mutable state changes.
/// </summary>
public abstract class MixedStateComponent<T, TMutableState> : ComputedStateComponent<T>
{
    private Action<State, StateEventKind>? _mutableStateChanged;

    protected MutableState<TMutableState> MutableState { get; private set; } = null!;

    public override ValueTask DisposeAsync()
    {
        var mutableStateChanged = _mutableStateChanged;
        if (mutableStateChanged is not null) {
            MutableState.Updated -= mutableStateChanged;
            _mutableStateChanged = null;
        }
        return base.DisposeAsync();
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (ReferenceEquals(MutableState, null))
            SetMutableState(CreateMutableState());
        return base.SetParametersAsync(parameters);
    }

    protected void SetMutableState(MutableState<TMutableState> mutableState)
    {
        if (MutableState is not null)
            throw Errors.AlreadyInitialized(nameof(MutableState));

        MutableState = mutableState ?? throw new ArgumentNullException(nameof(mutableState));
        var mutableStateChanged = _mutableStateChanged = (_, _) => _ = UntypedState.Recompute();
        mutableState.Updated += mutableStateChanged;
    }

    protected virtual string GetMutableStateCategory()
        => ComputedStateComponent.GetMutableStateCategory(GetType());

    protected virtual MutableState<TMutableState>.Options GetMutableStateOptions()
        => new() { Category = GetMutableStateCategory() };

    protected virtual MutableState<TMutableState> CreateMutableState()
        => StateFactory.NewMutable(GetMutableStateOptions());
}
