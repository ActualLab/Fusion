namespace ActualLab.Fusion.Blazor.Internal;

public sealed class DispatchingComputedStateComponentStateNoExecutionContextFlow<T>(
    ComputedState<T>.Options settings,
    ComputedStateComponent<T> component,
    IServiceProvider services
) : DispatchingComputedStateComponentState<T>(settings, component, services)
{
    public override ComputedStateDispatchMode DispatchMode
        => ComputedStateDispatchMode.Dispatch;

    protected override Task<T> Compute(CancellationToken cancellationToken)
        => Dispatcher.InvokeAsync(ComputeTaskFactory);
}
