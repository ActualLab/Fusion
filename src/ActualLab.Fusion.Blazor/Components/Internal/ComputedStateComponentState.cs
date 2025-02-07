namespace ActualLab.Fusion.Blazor.Internal;

public abstract class ComputedStateComponentState<T>(
    ComputedState<T>.Options settings,
    ComputedStateComponent<T> component,
    IServiceProvider services
    ) : ComputedState<T>(settings, services, false), IHasInitialize
{
    protected ComputedStateComponent<T> Component { get; } = component;

    public static ComputedStateComponentState<T> New(
        ComputedStateDispatchMode dispatchMode,
        Options settings,
        ComputedStateComponent<T> component,
        IServiceProvider services)
        => dispatchMode switch {
            ComputedStateDispatchMode.None
                => new NonDispatchingComputedStateComponentState<T>(settings, component, services),
            ComputedStateDispatchMode.Dispatch
                => new DispatchingComputedStateComponentStateNoExecutionContextFlow<T>(settings, component, services),
            ComputedStateDispatchMode.DispatchWithExecutionContextFlow
                => new DispatchingComputedStateComponentStateWithExecutionContextFlow<T>(settings, component, services),
            _ => throw new ArgumentOutOfRangeException(nameof(dispatchMode), dispatchMode, null)
        };

    public abstract ComputedStateDispatchMode DispatchMode { get; }

    void IHasInitialize.Initialize(object? settings)
        => base.Initialize((Options)settings!);
}
