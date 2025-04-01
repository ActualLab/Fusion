namespace ActualLab.Fusion.Blazor.Internal;

public sealed class NonDispatchingComputedStateComponentState<T>(
    ComputedState<T>.Options settings,
    ComputedStateComponent<T> component,
    IServiceProvider services
) : ComputedStateComponentState<T>(settings, component, services)
{
    public override ComputedStateDispatchMode DispatchMode
        => ComputedStateDispatchMode.None;

    protected override Task Compute(CancellationToken cancellationToken)
        => GetComputeTaskIfDisposed() ?? Component.ComputeState(cancellationToken);
}
