namespace ActualLab.Fusion.Blazor.Internal;

public sealed class DispatchingComputedStateComponentStateWithExecutionContextFlow<T>(
    ComputedState<T>.Options settings,
    ComputedStateComponent<T> component,
    IServiceProvider services
) : DispatchingComputedStateComponentState<T>(settings, component, services)
{
    public override ComputedStateDispatchMode DispatchMode
        => ComputedStateDispatchMode.DispatchWithExecutionContextFlow;

    protected override Task<T> Compute(CancellationToken cancellationToken)
    {
        var executionContext = ExecutionContext.Capture();
        return executionContext == null
            ? Dispatcher.InvokeAsync(ComputeTaskFactory)
            : Dispatcher.InvokeAsync(() => ExecutionContextExt.Start(executionContext, ComputeTaskFactory));
    }
}
