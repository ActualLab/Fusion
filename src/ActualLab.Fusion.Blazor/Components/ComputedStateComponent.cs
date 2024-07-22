using ActualLab.Fusion.Blazor.Internal;
using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

#pragma warning disable CA2007

public abstract class ComputedStateComponent<TState> : StatefulComponentBase<ComputedState<TState>>
{
    protected ComputedStateComponentOptions Options { get; set; } = ComputedStateComponent.DefaultOptions;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        var parameterSetIndex = ParameterSetIndex;
        var task = base.SetParametersAsync(parameters);
        var mustRecompute =
            (Options & ComputedStateComponentOptions.StateIsParameterDependent) != 0 // Requires recompute on parameter change
            && parameterSetIndex != 0 // Not the very first call to SetParametersAsync
            && ParameterSetIndex != parameterSetIndex; // And parameters were changed
        if (!mustRecompute)
            return task;

        if (task.IsCompletedSuccessfully) {
            _ = State.Recompute();
            return task;
        }

        return CompleteAsync(task);

        async Task CompleteAsync(Task dependency)
        {
            await dependency;
            _ = State.Recompute();
        }
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
