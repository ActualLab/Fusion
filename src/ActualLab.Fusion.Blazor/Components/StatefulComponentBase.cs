using Microsoft.AspNetCore.Components;
using ActualLab.Internal;

namespace ActualLab.Fusion.Blazor;

public abstract class StatefulComponentBase : FusionComponentBase, IAsyncDisposable
{
    private StateFactory? _stateFactory;

    [Inject] protected IServiceProvider Services { get; init; } = null!;

    protected StateFactory StateFactory => _stateFactory ??= Services.StateFactory();
    protected abstract IState UntypedState { get; }
    protected Action<IState, StateEventKind> StateChanged { get; set; }
    protected long RenderStateUpdateCount { get; set; } = -1;

    protected StatefulComponentBase()
    {
        MustRenderAfterEvent = false; // Typically these components render only after State change
        StateChanged = (_, _) => {
            if (UntypedState is IHasIsDisposed { IsDisposed: true })
                return;

            this.NotifyStateHasChanged();
        };
    }

    public virtual ValueTask DisposeAsync()
    {
        if (UntypedState is IDisposable d)
            d.Dispose();
        return default;
    }

    protected bool IsRenderStateChanged(IState renderState)
    {
        var renderStateUpdateCount = renderState.Snapshot.UpdateCount;
        if (RenderStateUpdateCount == renderStateUpdateCount)
            return false;

        RenderStateUpdateCount = renderStateUpdateCount;
        return true;
    }
}

public abstract class StatefulComponentBase<TState> : StatefulComponentBase
    where TState : class, IState
{
    protected TState State { get; private set; } = null!;
    protected override IState UntypedState => State;

    protected override void OnInitialized()
    {
        if (!ReferenceEquals(State, null))
            return;

        var (state, stateOptions) = CreateState();
        SetState(state, stateOptions);
    }

    protected virtual void SetState(
        TState state,
        object? stateOptions = null,
        StateEventKind stateChangedEventKind = StateEventKind.Updated)
    {
        if (!ReferenceEquals(State, null))
            throw Errors.AlreadyInitialized(nameof(State));

        State = state ?? throw new ArgumentNullException(nameof(state));
        state.AddEventHandler(stateChangedEventKind, StateChanged);
        if (stateOptions != null && state is IHasInitialize hasInitialize)
            hasInitialize.Initialize(stateOptions);
    }

    protected virtual (TState State, object? StateOptions) CreateState()
        => (Services.GetRequiredService<TState>(), null);
}
