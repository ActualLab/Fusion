using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public class StateFactory(IServiceProvider services) : IHasServices
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static StateFactory? _default;

    public static StateFactory Default {
        get {
            if (_default is { } value)
                return value;
            lock (StaticLock)
                return _default ??= new ServiceCollection().AddFusion().Services.BuildServiceProvider().StateFactory();
        }
        set {
            lock (StaticLock)
                _default = value;
        }
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
