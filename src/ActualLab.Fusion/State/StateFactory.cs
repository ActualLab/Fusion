using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public class StateFactory(IServiceProvider services) : IHasServices
{
    public IServiceProvider Services { get; } = services;

    public virtual MutableState<T> NewMutable<T>(MutableState<T>.Options settings)
        => new(settings, Services);

    public virtual ComputedState<T> NewComputed<T>(
        ComputedState<T>.Options settings,
        Func<CancellationToken, Task<T>> computer)
        => new FuncComputedState<T>(settings, Services, computer);

    public virtual ComputedState<T> NewComputed<T>(
        ComputedState<T>.Options settings,
        Func<ComputedState<T>, CancellationToken, Task<T>> computer)
        => new FuncComputedStateEx<T>(settings, Services, computer);
}
