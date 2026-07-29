using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

/// <summary>
/// A factory for creating <see cref="MutableState{T}"/> and <see cref="ComputedState{T}"/> instances.
/// </summary>
public class StateFactory(IServiceProvider services, bool isScoped) : IHasServices
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
            lock (StaticLock) {
                if (_default is { } newValue)
                    return newValue;

                newValue = new ServiceCollection().AddFusion().Services.BuildServiceProvider().StateFactory();
                Volatile.Write(ref _default, newValue);
                return newValue;
            }
        }
        set {
            lock (StaticLock)
                Volatile.Write(ref _default, value);
        }
    }

    public IServiceProvider Services { get; } = services;
    public bool IsScoped { get; } = isScoped;

    public virtual MutableState<T> NewMutable<T>(MutableState<T>.Options options)
        => new(options, Services);

    public virtual ComputedState<T> NewComputed<T>(
        ComputedState<T>.Options options,
        Func<CancellationToken, Task<T>> computer)
        => new FuncComputedState<T>(options, Services, computer);

    public virtual ComputedState<T> NewComputed<T>(
        ComputedState<T>.Options options,
        Func<ComputedState<T>, CancellationToken, Task<T>> computer)
        => new FuncComputedStateEx<T>(options, Services, computer);
}
