namespace ActualLab.Fusion.Blazor.Internal;

public sealed class DispatchingComputedStateComponentStateNoExecutionContextFlow<T>(
    ComputedState<T>.Options options,
    ComputedStateComponent<T> component,
    IServiceProvider services
) : DispatchingComputedStateComponentState<T>(options, component, services)
{
    public override ComputedStateDispatchMode DispatchMode
        => ComputedStateDispatchMode.Dispatch;

    protected override Task Compute(CancellationToken cancellationToken)
        => Dispatcher.InvokeAsync(ComputeTaskFactory);
}
