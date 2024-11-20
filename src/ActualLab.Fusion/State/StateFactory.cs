using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public class StateFactory(IServiceProvider services) : IHasServices
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif

    [field: AllowNull, MaybeNull]
    public static StateFactory Default {
        get {
            if (field is { } value)
                return value;
            lock (StaticLock)
                return field ??= new ServiceCollection().AddFusion().Services.BuildServiceProvider().StateFactory();
        }
        set {
            lock (StaticLock)
                field = value;
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
