using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public class StateFactory(IServiceProvider services) : IHasServices
{
    public IServiceProvider Services { get; } = services;

    public virtual MutableState<T> NewMutable<T>(MutableState<T>.Options settings)
        => new(settings, Services);

    public virtual ComputedState<T> NewComputed<T>(
        ComputedState<T>.Options settings,
        Func<IComputedState<T>, CancellationToken, Task<T>> computer)
        => new FuncComputedState<T>(settings, Services, computer);
}
