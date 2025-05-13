using ActualLab.Fusion.Blazor.Internal;

namespace ActualLab.Fusion.Blazor;

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
