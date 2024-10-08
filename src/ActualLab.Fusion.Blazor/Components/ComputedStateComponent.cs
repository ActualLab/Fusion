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
        bool mustRecompute;
        try {
            var task = base.SetParametersAsync(parameters);
            mustRecompute = (Options & ComputedStateComponentOptions.RecomputeStateOnParameterChange) != 0 // Requires recompute
                && parameterSetIndex != 0 // Not the very first call to SetParametersAsync
                && ParameterSetIndex != parameterSetIndex; // And parameters were changed
            if (!mustRecompute)
                return task;

            if (task.IsCompletedSuccessfully) {
                _ = State.Recompute();
                return task;
            }

            return CompleteAsync(task);

            async Task CompleteAsync(Task dependency) {
                await dependency;
                _ = State.Recompute();
            }
        }
        catch {
            // We can still conclude whether the parameters were changed or not.
            // There is nothing to await - all we need is to (maybe) recompute & throw.
            mustRecompute = (Options & ComputedStateComponentOptions.RecomputeStateOnParameterChange) != 0 // Requires recompute
                && parameterSetIndex != 0 // Not the very first call to SetParametersAsync
                && ParameterSetIndex != parameterSetIndex; // And parameters were changed
            if (mustRecompute)
                _ = State.Recompute();
            throw;
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
            Options.CanComputeStateOnThreadPool()
                ? ComputeState
                : stateOptions.FlowExecutionContext && DispatcherInfo.IsExecutionContextFlowSupported(this)
                    ? DispatchComputeState
                    : DispatchComputeStateWithManualExecutionContextFlow;
        return (new ComputedStateComponentState<TState>(stateOptions, computer, Services), stateOptions);

        Task<TState> DispatchComputeState(CancellationToken cancellationToken)
            => this.GetDispatcher().InvokeAsync(() => ComputeState(cancellationToken));

        Task<TState> DispatchComputeStateWithManualExecutionContextFlow(CancellationToken cancellationToken) {
            var executionContext = ExecutionContext.Capture();
            var dispatcher = this.GetDispatcher();
            var taskFactory = () => ComputeState(cancellationToken);
            return executionContext == null
                ? dispatcher.InvokeAsync(taskFactory)
                : dispatcher.InvokeAsync(() => ExecutionContextExt.Start(executionContext, taskFactory));
        }
    }

    protected abstract Task<TState> ComputeState(CancellationToken cancellationToken);

    protected override bool ShouldRender()
    {
        var computed = State.Computed;
        if (computed.IsConsistent() || computed.HasError)
            return true;

        // Inconsistent state is rare, so we make this check at last
        return (Options & ComputedStateComponentOptions.ShouldRenderInconsistentState) != 0;
    }
}
