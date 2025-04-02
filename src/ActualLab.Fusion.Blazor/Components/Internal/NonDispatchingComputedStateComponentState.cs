namespace ActualLab.Fusion.Blazor.Internal;

public sealed class NonDispatchingComputedStateComponentState<T>(
    ComputedState<T>.Options options,
    ComputedStateComponent<T> component,
    IServiceProvider services
) : ComputedStateComponentState<T>(options, component, services)
{
    public override ComputedStateDispatchMode DispatchMode
        => ComputedStateDispatchMode.None;

    protected override Task Compute(CancellationToken cancellationToken)
        => GetComputeTaskIfDisposed() ?? Component.ComputeState(cancellationToken);
}
