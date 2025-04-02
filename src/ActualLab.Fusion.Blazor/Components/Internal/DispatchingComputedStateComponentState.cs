using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor.Internal;

public abstract class DispatchingComputedStateComponentState<T>: ComputedStateComponentState<T>
{
    protected readonly Dispatcher Dispatcher;
    protected readonly Func<Task<T>> ComputeTaskFactory;

    protected DispatchingComputedStateComponentState(Options options, ComputedStateComponent<T> component, IServiceProvider services)
        : base(options, component, services)
    {
        Dispatcher = component.GetDispatcher();
        // We cache this delegate to avoid its allocation on per-compute basis.
        // It always uses GracefulDisposeToken (otherwise we'd have to create this delegate on per-call basis),
        // so we assume here that ComputeState:
        // - Is either called from UpdateCycle only,
        // - Or its other callers assume it may fail due to GracefulDisposeToken cancellation.
        ComputeTaskFactory = () => {
            var computeTaskIfDisposed = GetComputeTaskIfDisposed();
            return computeTaskIfDisposed != null
                ? (Task<T>)computeTaskIfDisposed
                : Component.ComputeState(GracefulDisposeToken);
        };
    }
}
