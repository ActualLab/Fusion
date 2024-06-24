using ActualLab.Fusion.Blazor.Internal;

namespace ActualLab.Fusion.Blazor;

public abstract class ComputedStateComponent<TState> : StatefulComponentBase<ComputedState<TState>>
{
    protected ComputedStateComponentOptions Options { get; init; } = ComputedStateComponent.DefaultOptions;

    // State frequently depends on component parameters, so...
    protected override Task OnParametersSetAsync()
    {
#pragma warning disable MA0040
        if (IsFirstSetParametersCallCompleted && (Options & ComputedStateComponentOptions.RecomputeOnParametersSet) != 0)
            _ = State.Recompute();

        return Task.CompletedTask;
#pragma warning restore MA0040
    }

    protected virtual ComputedState<TState>.Options GetStateOptions()
        => ComputedStateComponent.GetStateOptions<TState>(GetType());

    protected override (ComputedState<TState> State, object? StateOptions) CreateState()
    {
        // Synchronizes ComputeState call as per:
        // https://github.com/servicetitan/Stl.Fusion/issues/202
        var stateOptions = GetStateOptions();
        Func<CancellationToken, Task<TState>> computer =
            (Options & ComputedStateComponentOptions.SynchronizeComputeState) == 0
                ? UnsynchronizedComputeState
                : stateOptions.FlowExecutionContext && DispatcherInfo.IsExecutionContextFlowSupported(this)
                    ? SynchronizedComputeState
                    : SynchronizedComputeStateWithManualExecutionContextFlow;
        return (new ComputedStateComponentState<TState>(stateOptions, computer, Services), stateOptions);

        Task<TState> UnsynchronizedComputeState(CancellationToken cancellationToken)
            => ComputeState(cancellationToken);

        Task<TState> SynchronizedComputeState(CancellationToken cancellationToken)
            => this.GetDispatcher().InvokeAsync(() => ComputeState(cancellationToken));

        Task<TState> SynchronizedComputeStateWithManualExecutionContextFlow(CancellationToken cancellationToken)
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
