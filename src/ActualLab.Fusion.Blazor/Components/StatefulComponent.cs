using ActualLab.Internal;
using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

public interface IStatefulComponent : IHasCircuitHub, IAsyncDisposable
{
    public State State { get; }
}

public interface IStatefulComponent<T> : IStatefulComponent
{
    public new IState<T> State { get; }
}

public abstract class StatefulComponentBase : CircuitHubComponentBase, IStatefulComponent
{
    protected State State { get; private set; } = null!;
    protected Action<State, StateEventKind> StateChanged { get; set; }

    // Explicit IState implementation
    State IStatefulComponent.State => State;

    protected StatefulComponentBase()
    {
        MustRenderAfterEvent = false; // Typically these components render only after State change
        StateChanged = (_, _) => {
            if (State is IHasIsDisposed { IsDisposed: true })
                return;

            this.NotifyStateHasChanged();
        };
    }

    public virtual ValueTask DisposeAsync()
    {
        if (State is IDisposable d)
            d.Dispose();
        return default;
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        var task = base.SetParametersAsync(parameters);
        // If we're here:
        // - Sync part of OnInitialized(Async) is completed
        // - Sync part of OnParametersSet(Async) is completed
        // - No error is thrown from the sync part of that code
        if (!ReferenceEquals(State, null))
            return task;

        var (state, stateOptions) = CreateState();
        SetState(state, stateOptions);
        return task;
    }

    protected void SetState(
        IState state,
        StateEventKind stateChangedEventKind = StateEventKind.Updated)
        => SetState((State)state, null, stateChangedEventKind);

    protected void SetState(
        IState state,
        object? stateInitializeOptions,
        StateEventKind stateChangedEventKind = StateEventKind.Updated)
        => SetState((State)state, stateInitializeOptions, stateChangedEventKind);

    protected void SetState(
        State state,
        StateEventKind stateChangedEventKind = StateEventKind.Updated)
        => SetState(state, null, stateChangedEventKind);

    protected virtual void SetState(
        State state,
        object? stateInitializeOptions,
        StateEventKind stateChangedEventKind = StateEventKind.Updated)
    {
        if (!ReferenceEquals(State, null))
            throw Errors.AlreadyInitialized(nameof(State));

        State = state ?? throw new ArgumentNullException(nameof(state));
        state.AddEventHandler(stateChangedEventKind, StateChanged);
        if (stateInitializeOptions != null && state is IHasInitialize hasInitialize)
            hasInitialize.Initialize(stateInitializeOptions);
    }

    protected virtual (State State, object? StateInitializeOptions) CreateState()
#pragma warning disable MA0025
        => throw new NotImplementedException(
            "CreateState is called when SetParametersAsync doesn't call SetState. " +
            "You must override either this method, OnInitialized, or SetParametersAsync to make sure State is set.");
#pragma warning restore MA0025
}

public abstract class StatefulComponentBase<T> : StatefulComponentBase, IStatefulComponent<T>
{
    protected State UntypedState => base.State;
    protected new IState<T> State => (IState<T>)base.State;
    IState<T> IStatefulComponent<T>.State => (IState<T>)base.State;
}
