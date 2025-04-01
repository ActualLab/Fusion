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

            var mustAwaitForRecompute = (Options & ComputedStateComponentOptions.AwaitForRecomputeOnParameterChange) != 0;
            if (!task.IsCompletedSuccessfully)
                return CompleteAsync(task, State, mustAwaitForRecompute);

            var recomputeTask = State.Recompute();
            return mustAwaitForRecompute && !recomputeTask.IsCompleted
                ? CompleteRecomputeAsync(recomputeTask.AsTask())
                : Task.CompletedTask;

            static async Task CompleteAsync(Task dependency, ComputedState<TState> state, bool mustAwaitForRecompute1) {
                await dependency;
                var recomputeTask1 = state.Recompute();
                if (mustAwaitForRecompute1 && !recomputeTask1.IsCompleted)
                    await recomputeTask1.SilentAwait();
            }

            static async Task CompleteRecomputeAsync(Task recomputeTask1)
                => await recomputeTask1.SilentAwait();
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
        var dispatchMode = Options.CanComputeStateOnThreadPool()
            ? ComputedStateDispatchMode.None
            : stateOptions.FlowExecutionContext && DispatcherInfo.IsExecutionContextFlowSupported(this)
                ? ComputedStateDispatchMode.Dispatch
                : ComputedStateDispatchMode.DispatchWithExecutionContextFlow;
        var state = ComputedStateComponentState<TState>.New(dispatchMode, stateOptions, this, Services);
        return (state, stateOptions);
    }

    protected internal abstract Task<TState> ComputeState(CancellationToken cancellationToken);

    protected override bool ShouldRender()
    {
        var computed = State.Computed;
        if (computed.IsConsistent() || computed.HasError)
            return true;

        // Inconsistent state is rare, so we make this check at last
        return (Options & ComputedStateComponentOptions.RenderInconsistentState) != 0;
    }
}
