using ActualLab.Fusion.Blazor.Internal;
using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

public abstract partial class ComputedStateComponent : StatefulComponentBase
{
    protected ComputedStateComponentOptions Options { get; set; } = DefaultOptions;

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
                ? recomputeTask.AsTask().SuppressExceptions()
                : Task.CompletedTask;

            static async Task CompleteAsync(Task dependency, State state, bool mustAwaitForRecompute1) {
                try {
                    await dependency.ConfigureAwait(false); // Ok here
                }
                finally {
                    var recomputeTask1 = state.Recompute();
                    if (mustAwaitForRecompute1 && !recomputeTask1.IsCompleted)
                        await recomputeTask1.SilentAwait();
                }
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

    protected override bool ShouldRender()
    {
        var computed = State.Computed;
        if (computed.IsConsistent() || computed.HasError)
            return true;

        // Inconsistent state is rare, so we make this check at last
        return (Options & ComputedStateComponentOptions.RenderInconsistentState) != 0;
    }
}

public abstract class ComputedStateComponent<T> : ComputedStateComponent, IStatefulComponent<T>
{
    protected State UntypedState => base.State;
    protected new ComputedState<T> State => (ComputedState<T>)base.State;
    IState<T> IStatefulComponent<T>.State => (IState<T>)base.State;

    protected virtual ComputedState<T>.Options GetStateOptions()
        => ComputedStateComponent.GetStateOptions<T>(GetType());

    protected override (State State, object? StateInitializeOptions) CreateState()
    {
        // Synchronizes ComputeState call as per:
        // https://github.com/servicetitan/Stl.Fusion/issues/202
        var stateOptions = GetStateOptions();
        var dispatchMode = Options.CanComputeStateOnThreadPool()
            ? ComputedStateDispatchMode.None
            : stateOptions.FlowExecutionContext && DispatcherInfo.IsExecutionContextFlowSupported(this)
                ? ComputedStateDispatchMode.Dispatch
                : ComputedStateDispatchMode.DispatchWithExecutionContextFlow;
        var state = ComputedStateComponentState<T>.New(dispatchMode, stateOptions, this, Services);
        return (state, stateOptions);
    }

    protected internal abstract Task<T> ComputeState(CancellationToken cancellationToken);
}
