using Microsoft.AspNetCore.Components;
using ActualLab.Internal;

namespace ActualLab.Fusion.Blazor;

public abstract class StatefulComponentBase : FusionComponentBase, IAsyncDisposable, IHandleEvent
{
    private StateFactory? _stateFactory;

    [Inject] protected IServiceProvider Services { get; init; } = null!;

    protected StateFactory StateFactory => _stateFactory ??= Services.StateFactory();
    protected abstract IState UntypedState { get; }
    protected Action<IState, StateEventKind> StateChanged { get; set; }
    protected long PureRenderStateUpdateCount { get; set; } = -1;

    // It's typically more natural for stateful components to recompute State
    // and trigger StateHasChanged only as a result state (re)computation or parameter changes.
    protected bool MustCallStateHasChangedAfterEvent { get; set; } = false;

    protected StatefulComponentBase()
        => StateChanged = (_, _) => {
            if (UntypedState is IHasIsDisposed { IsDisposed: true })
                return;

            this.NotifyStateHasChanged();
        };

    public virtual ValueTask DisposeAsync()
    {
        if (UntypedState is IDisposable d)
            d.Dispose();
        return default;
    }

    protected bool IsPureRenderStateChanged(IState pureRenderState)
    {
        var pureRenderStateUpdateCount = pureRenderState.Snapshot.UpdateCount;
        if (PureRenderStateUpdateCount == pureRenderStateUpdateCount)
            return false;

        PureRenderStateUpdateCount = pureRenderStateUpdateCount;
        return true;
    }

    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
    {
        // This code provides support for EnableStateHasChangedCallAfterEvent option
        // See https://github.com/dotnet/aspnetcore/issues/18919#issuecomment-803005864
        var task = callback.InvokeAsync(arg);
        var shouldAwaitTask =
            task.Status != TaskStatus.RanToCompletion &&
            task.Status != TaskStatus.Canceled;
        if (shouldAwaitTask)
            return CallStateHasChangedOnAsyncCompletion(task);

#pragma warning disable VSTHRD103
#pragma warning disable MA0042
        if (MustCallStateHasChangedAfterEvent)
            StateHasChanged();
#pragma warning restore MA0042
#pragma warning restore VSTHRD103
        return Task.CompletedTask;
    }

    private async Task CallStateHasChangedOnAsyncCompletion(Task task)
    {
        try {
            await task.ConfigureAwait(false);
        }
        catch {
            // Avoiding exception filters for AOT runtime support.
            // Ignore exceptions from task cancelletions, but don't bother issuing a state change.
            if (task.IsCanceled)
                return;
            throw;
        }
#pragma warning disable VSTHRD103
#pragma warning disable MA0042
        if (MustCallStateHasChangedAfterEvent)
            StateHasChanged();
#pragma warning restore MA0042
#pragma warning restore VSTHRD103
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
