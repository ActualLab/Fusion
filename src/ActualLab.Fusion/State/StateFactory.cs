using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public class StateFactory(IServiceProvider services) : IHasServices
{
    private static readonly Lock Lock = new();
    private static StateFactory? _default;

    public static StateFactory Default {
        get {
            if (_default != null)
                return _default;

            lock (Lock) {
                _default ??= new ServiceCollection().AddFusion().Services.BuildServiceProvider().StateFactory();
                return _default;
            }
        }
        set => _default = value;
    }

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
