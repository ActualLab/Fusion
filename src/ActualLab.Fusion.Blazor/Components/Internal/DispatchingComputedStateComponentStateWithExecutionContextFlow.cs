namespace ActualLab.Fusion.Blazor.Internal;

/// <summary>
/// A dispatching computed state that manually captures and flows ExecutionContext
/// into the dispatcher callback, used when the dispatcher does not natively flow it.
/// </summary>
public sealed class DispatchingComputedStateComponentStateWithExecutionContextFlow<T>(
    ComputedState<T>.Options options,
    ComputedStateComponent<T> component,
    IServiceProvider services
) : DispatchingComputedStateComponentState<T>(options, component, services)
{
    public override ComputedStateDispatchMode DispatchMode
        => ComputedStateDispatchMode.DispatchWithExecutionContextFlow;

    protected override Task Compute(CancellationToken cancellationToken)
    {
        var executionContext = ExecutionContext.Capture();
        return executionContext is null
            ? Dispatcher.InvokeAsync(ComputeTaskFactory)
            : Dispatcher.InvokeAsync(() => ExecutionContextExt.Start(executionContext, ComputeTaskFactory));
    }
}
