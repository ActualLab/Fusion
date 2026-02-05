namespace ActualLab.Fusion.Blazor.Internal;

/// <summary>
/// A dispatching computed state that does not flow ExecutionContext,
/// used when the dispatcher natively supports ExecutionContext flow.
/// </summary>
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
