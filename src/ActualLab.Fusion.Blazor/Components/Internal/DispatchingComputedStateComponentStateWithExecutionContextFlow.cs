namespace ActualLab.Fusion.Blazor.Internal;

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
