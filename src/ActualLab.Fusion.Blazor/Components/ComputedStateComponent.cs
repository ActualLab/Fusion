using ActualLab.Fusion.Blazor.Internal;

namespace ActualLab.Fusion.Blazor;

public abstract class ComputedStateComponent<TState> : StatefulComponentBase<IComputedState<TState>>
{
    protected ComputedStateComponentOptions Options { get; init; } = ComputedStateComponent.DefaultOptions;

    // State frequently depends on component parameters, so...
    protected override Task OnParametersSetAsync()
    {
        if ((Options & ComputedStateComponentOptions.RecomputeOnParametersSet) == 0)
            return Task.CompletedTask;
        _ = State.Recompute();
        return Task.CompletedTask;
    }

    protected virtual ComputedState<TState>.Options GetStateOptions()
        => ComputedStateComponent.GetStateOptions<TState>(GetType());

    protected override (IComputedState<TState> State, object? StateOptions) CreateState()
    {
        // Synchronizes ComputeState call as per:
        // https://github.com/servicetitan/Stl.Fusion/issues/202
        var stateOptions = GetStateOptions();
        Func<IComputedState<TState>, CancellationToken, Task<TState>> computer =
            (Options & ComputedStateComponentOptions.SynchronizeComputeState) == 0
                ? UnsynchronizedComputeState
                : stateOptions.FlowExecutionContext && DispatcherInfo.IsExecutionContextFlowSupported(this)
                    ? SynchronizedComputeState
                    : SynchronizedComputeStateWithManualExecutionContextFlow;
        return (new ComputedStateComponentState<TState>(stateOptions, computer, Services), stateOptions);

        Task<TState> UnsynchronizedComputeState(
            IComputedState<TState> state, CancellationToken cancellationToken)
            => ComputeState(cancellationToken);

        Task<TState> SynchronizedComputeState(
            IComputedState<TState> state, CancellationToken cancellationToken)
            => this.GetDispatcher().InvokeAsync(() => ComputeState(cancellationToken));

        Task<TState> SynchronizedComputeStateWithManualExecutionContextFlow(
            IComputedState<TState> state, CancellationToken cancellationToken)
        {
            var executionContext = ExecutionContext.Capture();
            var taskFactory = () => ComputeState(cancellationToken);
            return executionContext == null
                ? this.GetDispatcher().InvokeAsync(taskFactory)
                : this.GetDispatcher().InvokeAsync(() => ExecutionContextExt.Start(executionContext, taskFactory));
        }
    }

    protected abstract Task<TState> ComputeState(CancellationToken cancellationToken);
}
